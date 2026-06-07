using System.Text;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Application.Services;
using MGold.Domain.Constants;
using MGold.Domain.Enums;
using MGold.Domain.Entities;
using MGold.Infrastructure.Data;
using MGold.Infrastructure.Http;
using MGold.Infrastructure.Repositories;
using MGold.Infrastructure.Repositories.Interfaces;
using MGold.Hubs;
using MGold.Middleware;

var builder = WebApplication.CreateBuilder(args);
var baseUrl = ResolvePreferredBaseUrl(builder.Configuration, args);
if (!string.IsNullOrWhiteSpace(baseUrl))
{
    // Respect explicit app configuration only when no higher-priority URL source is provided.
    builder.WebHost.UseUrls(baseUrl);
}

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
if (builder.Environment.IsDevelopment())
{
    var keyDirectory = Path.Combine(builder.Environment.ContentRootPath, ".keys");
    Directory.CreateDirectory(keyDirectory);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
        .SetApplicationName("MGold");
}
builder.Services.AddSignalR();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
    {
        var key = $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}:{context.Request.Path}";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.AddPolicy("sms", context =>
    {
        var key = $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}:{context.Request.Path}";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.AddPolicy("payment", context =>
    {
        var user = context.User?.Identity?.Name;
        var key = $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}:{user ?? "anonymous"}:{context.Request.Path}";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("tr-TR"),
        new CultureInfo("en-US")
    };

    options.DefaultRequestCulture = new RequestCulture("tr-TR");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

// Centralized model validation keeps controller actions lean and consistent.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid request." : e.ErrorMessage)
            .Distinct()
            .ToArray();

        return new BadRequestObjectResult(new MGold.Common.ApiResponse<object>
        {
            Success = false,
            Message = "Validation failed.",
            Errors = errors
        });
    };
});

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.Configure<SmsSettings>(builder.Configuration.GetSection(SmsSettings.SectionName));
builder.Services.Configure<CompanyProfileSettings>(builder.Configuration.GetSection(CompanyProfileSettings.SectionName));
builder.Services.Configure<MarketDataSettings>(builder.Configuration.GetSection(MarketDataSettings.SectionName));
builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
    options.ShutdownTimeout = TimeSpan.FromSeconds(20);
});
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
if (!builder.Environment.IsDevelopment())
{
    var invalidJwt = string.IsNullOrWhiteSpace(jwtSettings.Key)
        || jwtSettings.Key.Contains("CHANGE_THIS", StringComparison.OrdinalIgnoreCase)
        || jwtSettings.Key.Length < 32;
    if (invalidJwt)
    {
        throw new InvalidOperationException("Production JWT key is invalid. Configure a strong Jwt:Key (min 32 chars).");
    }
}
var key = Encoding.UTF8.GetBytes(jwtSettings.Key);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "SmartScheme";
        options.DefaultAuthenticateScheme = "SmartScheme";
        options.DefaultChallengeScheme = "SmartScheme";
    })
    .AddPolicyScheme("SmartScheme", "JWT or Cookie", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return JwtBearerDefaults.AuthenticationScheme;
            }

            return CookieAuthenticationDefaults.AuthenticationScheme;
        };
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/auth/login";
        options.AccessDeniedPath = "/auth";
        options.Cookie.Name = "MGold.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                var target = ResolveInteractiveLoginPath(context.Request.Path);
                context.Response.Redirect($"{target}?returnUrl={Uri.EscapeDataString(context.Request.Path + context.Request.QueryString)}");
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                var target = ResolveInteractiveLoginPath(context.Request.Path);
                context.Response.Redirect(target);
                return Task.CompletedTask;
            }
        };
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var securityStamp = context.Principal?.FindFirst("security_stamp")?.Value;
                if (!int.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(securityStamp))
                {
                    context.Fail("Invalid token claims.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var user = await db.AppUsers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == userId, context.HttpContext.RequestAborted);
                if (user is null || !user.IsActive || !user.EmailConfirmed || user.SecurityStamp != securityStamp)
                {
                    context.Fail("Token is no longer valid.");
                }
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                var message = context.AuthenticateFailure is SecurityTokenExpiredException
                    ? "Token expired. Please login again."
                    : "Unauthorized. Valid token is required.";
                await context.Response.WriteAsJsonAsync(new MGold.Common.ApiResponse<object>
                {
                    Success = false,
                    Message = message
                });
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new MGold.Common.ApiResponse<object>
                {
                    Success = false,
                    Message = "Forbidden. You do not have permission for this resource."
                });
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddAntiforgery();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiCors", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                 uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)))
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }
    });
});

var useSqlite = builder.Configuration.GetValue<bool>("App:UseSqlite");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSqlite)
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("SqliteConnection"));
    }
    else
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
});
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IGoldPriceRepository, GoldPriceRepository>();
builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IGoldPriceService, GoldPriceService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IOrderHistoryService, OrderHistoryService>();
builder.Services.AddScoped<IProductReviewService, ProductReviewService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IMarketDataService, MarketDataService>();
builder.Services.AddScoped<IMarketDataValidator, MarketDataValidator>();
builder.Services.AddScoped<IWorkforceService, WorkforceService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAccountVerificationService, AccountVerificationService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IPaymentProvider, ManualPaymentProvider>();
builder.Services.AddScoped<IPaymentProviderRegistry, PaymentProviderRegistry>();
builder.Services.AddTransient<TransientHttpRetryHandler>();
builder.Services.AddHttpClient<IMarketDataProvider, LiveMarketApiProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
}).AddHttpMessageHandler<TransientHttpRetryHandler>();
builder.Services.AddHttpClient<ISmsService, NetgsmSmsService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<TransientHttpRetryHandler>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthRouteService, AuthRouteService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAccessControlService, AccessControlService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<AuditLogActionFilter>();
builder.Services.AddHostedService<MarketRefreshWorker>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});
builder.Logging.AddDebug();

var app = builder.Build();
var enableHttpsRedirection = builder.Configuration.GetValue<bool>("App:EnableHttpsRedirection");
var autoMigrate = builder.Configuration.GetValue<bool>("App:AutoMigrate");
var seedDemoData = builder.Configuration.GetValue<bool>("App:SeedDemoData");
using var singleInstanceGuard = SingleInstanceGuard.TryAcquire(app.Environment, useSqlite, app.Configuration.GetConnectionString("SqliteConnection"));
if (!singleInstanceGuard.Acquired)
{
    app.Logger.LogWarning("Another MGold instance is already running for this environment. The new process will exit without rebinding the port.");
    return;
}

if (!string.IsNullOrWhiteSpace(baseUrl) && !CanBindToConfiguredUrl(baseUrl))
{
    app.Logger.LogWarning("Configured App:BaseUrl {BaseUrl} is already in use. Keeping the existing instance and stopping this process.", baseUrl);
    return;
}

ValidateProductionSecrets(app.Configuration, app.Environment, useSqlite);

app.UseMiddleware<RequestCorrelationMiddleware>();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers.TryAdd("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }

    await next();
});
app.UseRequestLocalization();
app.UseDefaultFiles();
app.UseStaticFiles();

if (enableHttpsRedirection)
{
    app.UseHttpsRedirection();
}

app.UseCors("ApiCors");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<AuthRouteGuardMiddleware>();
app.UseAuthorization();

app.MapGet("/api/health", (HttpContext context) => Results.Ok(new
{
    status = "ok",
    utc = DateTime.UtcNow,
    correlationId = RequestCorrelationMiddleware.GetCorrelationId(context)
}));
app.MapHealthChecks("/api/health/ready", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            correlationId = RequestCorrelationMiddleware.GetCorrelationId(context),
            checks = report.Entries.Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                durationMs = x.Value.Duration.TotalMilliseconds
            })
        });
    }
});
app.MapHealthChecks("/api/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapGet("/api", () => Results.Ok(new { message = "MGold API is running." }));
app.MapGet("/", () => Results.Redirect("/auth"));

app.MapControllers();
app.MapHub<MarketHub>("/hubs/market");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Public}/{action=Index}/{id?}");

InitializeDatabase(app, useSqlite, autoMigrate);
SeedDatabase(app, seedDemoData);

app.Run();

static string? ResolvePreferredBaseUrl(IConfiguration configuration, string[] args)
{
    var hasExplicitUrls = args.Any(arg =>
        arg.StartsWith("--urls", StringComparison.OrdinalIgnoreCase)
        || arg.StartsWith("--server.urls", StringComparison.OrdinalIgnoreCase));
    if (hasExplicitUrls)
    {
        return null;
    }

    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    {
        return null;
    }

    var configured = configuration["App:BaseUrl"];
    return string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();
}

static bool CanBindToConfiguredUrl(string baseUrl)
{
    if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Port <= 0)
    {
        return true;
    }

    try
    {
        var host = string.IsNullOrWhiteSpace(uri.Host) || uri.Host == "0.0.0.0"
            ? IPAddress.Loopback
            : Dns.GetHostAddresses(uri.Host)
                .FirstOrDefault(address => address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                ?? IPAddress.Loopback;
        using var socket = new Socket(host.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(host, uri.Port));
        return true;
    }
    catch (SocketException)
    {
        return false;
    }
    catch
    {
        return true;
    }
}

static void ValidateProductionSecrets(IConfiguration configuration, IHostEnvironment environment, bool useSqlite)
{
    if (environment.IsDevelopment())
    {
        return;
    }

    if (useSqlite)
    {
        throw new InvalidOperationException("Production cannot use SQLite. Configure App:UseSqlite=false.");
    }

    var defaultConnection = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    if (defaultConnection.Contains("Your_strong_password123", StringComparison.OrdinalIgnoreCase)
        || defaultConnection.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Production database connection string contains placeholder secrets.");
    }

    var email = configuration.GetSection(EmailSettings.SectionName).Get<EmailSettings>() ?? new EmailSettings();
    if (email.Enabled && (string.IsNullOrWhiteSpace(email.Host)
        || string.IsNullOrWhiteSpace(email.Username)
        || string.IsNullOrWhiteSpace(email.Password)
        || email.Password.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException("Production email settings are incomplete.");
    }

    var sms = configuration.GetSection(SmsSettings.SectionName).Get<SmsSettings>() ?? new SmsSettings();
    if (sms.Enabled && (string.IsNullOrWhiteSpace(sms.Username)
        || string.IsNullOrWhiteSpace(sms.Password)
        || string.IsNullOrWhiteSpace(sms.Originator)))
    {
        throw new InvalidOperationException("Production SMS settings are incomplete.");
    }
}

static void InitializeDatabase(WebApplication app, bool useSqlite, bool autoMigrate)
{
    if (!autoMigrate)
    {
        return;
    }

    if (app.Environment.IsDevelopment() && useSqlite)
    {
        InitializeDevelopmentSqliteDatabase(app);
        return;
    }

    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex) when (app.Environment.IsDevelopment() && useSqlite)
    {
        app.Logger.LogWarning(ex, "SQLite development database could not be migrated. Resetting the local database file.");
        SqliteConnection.ClearAllPools();
        ResetSqliteDevelopmentDatabase(app.Configuration.GetConnectionString("SqliteConnection"));

        using var retryScope = app.Services.CreateScope();
        var retryDb = retryScope.ServiceProvider.GetRequiredService<AppDbContext>();
        retryDb.Database.Migrate();
    }
}

static void InitializeDevelopmentSqliteDatabase(WebApplication app)
{
    var connectionString = app.Configuration.GetConnectionString("SqliteConnection");
    var databasePath = GetSqliteDatabasePath(connectionString);
    var shouldRecreate = !string.IsNullOrWhiteSpace(databasePath) && !File.Exists(databasePath);

    if (!shouldRecreate)
    {
        shouldRecreate = !HasRequiredSqliteTables(connectionString);
    }

    if (shouldRecreate)
    {
        SqliteConnection.ClearAllPools();
        ResetSqliteDevelopmentDatabase(connectionString);
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    ApplySqliteDevelopmentUpgrades(connectionString);
}

static void SeedDatabase(WebApplication app, bool seedDemoData)
{
    if (!seedDemoData)
    {
        SeedProductionEssentials(app);
        return;
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
    var demoCompanyId = EnsureCompany(
        db,
        name: "MGold Demo Store",
        code: "MGOLD-DEMO",
        email: "demo@mgold.local",
        phone: "+902125555555",
        address: "Kapalicarsi, Fatih / Istanbul");
    if (!db.GoldPrices.Any())
    {
        db.GoldPrices.Add(new GoldPrice
        {
            PricePerGram = 4250m,
            EffectiveFrom = DateTime.UtcNow,
            Source = "Demo seed",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    SeedUsersAndAccounts(db, passwordHasher);
    SeedMarketProviders(db);

    if (db.Products.Any())
    {
        SeedAdditionalDemoData(db);
        return;
    }

    db.Products.AddRange(
        new Product
        {
            CompanyId = demoCompanyId,
            Name = "22 Ayar Burgu Bilezik",
            Type = ProductType.Gold,
            Weight = 18.45m,
            PurityRate = 0.9167m,
            LaborCost = 850m,
            LaborCostPercentage = 2.5m,
            AdditionalCost = 120m,
            ProfitMarginPercentage = 8m,
            PurchasePrice = 74250m,
            SalePrice = 80990m,
            StockQuantity = 6
        },
        new Product
        {
            CompanyId = demoCompanyId,
            Name = "14 Ayar Zincir Kolye",
            Type = ProductType.Gold,
            Weight = 11.20m,
            PurityRate = 0.5850m,
            LaborCost = 520m,
            LaborCostPercentage = 1.8m,
            AdditionalCost = 70m,
            ProfitMarginPercentage = 10m,
            PurchasePrice = 29800m,
            SalePrice = 33890m,
            StockQuantity = 12
        },
        new Product
        {
            CompanyId = demoCompanyId,
            Name = "Minimal Pirlanta Tek Tas",
            Type = ProductType.Diamond,
            Weight = 3.60m,
            PurityRate = 1m,
            LaborCost = 1200m,
            LaborCostPercentage = 0m,
            AdditionalCost = 450m,
            ProfitMarginPercentage = 15m,
            PurchasePrice = 45500m,
            SalePrice = 53900m,
            StockQuantity = 4
        },
        new Product
        {
            CompanyId = demoCompanyId,
            Name = "Gumus Erkek Bileklik",
            Type = ProductType.Silver,
            Weight = 24.10m,
            PurityRate = 0.925m,
            LaborCost = 240m,
            LaborCostPercentage = 1.2m,
            AdditionalCost = 40m,
            ProfitMarginPercentage = 12m,
            PurchasePrice = 1450m,
            SalePrice = 1890m,
            StockQuantity = 15
        },
        new Product
        {
            CompanyId = demoCompanyId,
            Name = "Zarif Baget Pırlanta Yüzük",
            Type = ProductType.Diamond,
            Weight = 2.85m,
            PurityRate = 0.95m,
            LaborCost = 950m,
            LaborCostPercentage = 0m,
            AdditionalCost = 320m,
            ProfitMarginPercentage = 18m,
            PurchasePrice = 38200m,
            SalePrice = 46900m,
            StockQuantity = 6   
        },
        new Product
        {
            CompanyId = demoCompanyId,
            Name = "Vintage Safir Taşlı Kolye",
            Type = ProductType.Gold,
            Weight = 5.20m,
            PurityRate = 0.92m,
            LaborCost = 1750m,
            LaborCostPercentage = 0m,
            AdditionalCost = 680m,
            ProfitMarginPercentage = 22m,
            PurchasePrice = 61800m,
            SalePrice = 78900m,
            StockQuantity = 3
            
        }
        );

    db.SaveChanges();
    SeedAdditionalDemoData(db);
}

static void SeedProductionEssentials(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
        var companyProfile = app.Configuration.GetSection(CompanyProfileSettings.SectionName).Get<CompanyProfileSettings>() ?? new CompanyProfileSettings();
        var companyId = EnsureCompany(
            db,
            name: string.IsNullOrWhiteSpace(companyProfile.Name) ? "MGold Kuyumculuk" : companyProfile.Name,
            code: "MGOLD",
            email: string.IsNullOrWhiteSpace(companyProfile.Email) ? "info@mgold.local" : companyProfile.Email,
            phone: string.IsNullOrWhiteSpace(companyProfile.Phone) ? "+905550000000" : companyProfile.Phone,
            address: string.IsNullOrWhiteSpace(companyProfile.Address) ? "Istanbul" : companyProfile.Address);

        EnsureProductionBootstrapAccounts(db, passwordHasher, app.Configuration);
        SeedMarketProviders(db);

        app.Logger.LogInformation("Production essentials checked. Bootstrap company id: {CompanyId}", companyId);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Production essentials could not be seeded. Run the production SQL install script before using database-backed features.");
    }
}

static void ResetSqliteDevelopmentDatabase(string? connectionString)
{
    var databasePath = GetSqliteDatabasePath(connectionString);
    if (string.IsNullOrWhiteSpace(databasePath))
    {
        return;
    }

    if (File.Exists(databasePath))
    {
        File.Delete(databasePath);
    }
}

static string? GetSqliteDatabasePath(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return null;
    }

    var builder = new SqliteConnectionStringBuilder(connectionString);
    if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
    {
        return null;
    }

    return Path.GetFullPath(builder.DataSource, Directory.GetCurrentDirectory());
}

static bool HasRequiredSqliteTables(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return false;
    }

    var requiredTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Companies",
        "Products",
        "Customers",
        "Transactions",
        "GoldPrices",
        "AppUsers",
        "AuditLogs",
        "Orders",
        "OrderItems",
        "OrderPayments",
        "OrderInvoices",
        "OrderHistoryEntries",
        "CustomerFavorites",
        "ProductReviews",
        "Notifications",
        "ContactMessages",
        "MarketProviderConfigurations",
        "MarketQuoteSnapshots",
        "MarketWatchlistItems",
        "WorkTasks",
        "WorkTaskHistoryEntries",
        "AccountVerificationTokens",
        "RefreshTokens"
    };

    try
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(0);
            requiredTables.Remove(name);
        }

        return requiredTables.Count == 0;
    }
    catch
    {
        return false;
    }
}

static void ApplySqliteDevelopmentUpgrades(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return;
    }

    using var connection = new SqliteConnection(connectionString);
    connection.Open();

    EnsureCompaniesTable(connection);
    EnsureSqliteColumn(connection, "AppUsers", "Email", "TEXT NOT NULL DEFAULT ''");
    EnsureSqliteColumn(connection, "AppUsers", "Phone", "TEXT NOT NULL DEFAULT ''");
    EnsureSqliteColumn(connection, "AppUsers", "CustomerId", "INTEGER NULL");
    EnsureSqliteColumn(connection, "AppUsers", "CompanyId", "INTEGER NULL");
    EnsureSqliteColumn(connection, "AppUsers", "CreatedByUserId", "INTEGER NULL");
    EnsureSqliteColumn(connection, "AppUsers", "EmailConfirmed", "INTEGER NOT NULL DEFAULT 1");
    EnsureSqliteColumn(connection, "AppUsers", "EmailConfirmedAt", "TEXT NULL");
    EnsureSqliteColumn(connection, "AppUsers", "PhoneConfirmed", "INTEGER NOT NULL DEFAULT 0");
    EnsureSqliteColumn(connection, "AppUsers", "PhoneConfirmedAt", "TEXT NULL");
    EnsureSqliteColumn(connection, "AppUsers", "AccessFailedCount", "INTEGER NOT NULL DEFAULT 0");
    EnsureSqliteColumn(connection, "AppUsers", "LockoutEndAt", "TEXT NULL");
    EnsureSqliteColumn(connection, "AppUsers", "SecurityStamp", "TEXT NOT NULL DEFAULT ''");
    EnsureSqliteColumn(connection, "AppUsers", "ThemePreference", "TEXT NOT NULL DEFAULT 'gold-premium'");
    EnsureSqliteColumn(connection, "AppUsers", "PasswordChangedAt", "TEXT NULL");
    EnsureSqliteColumn(connection, "Customers", "CompanyId", "INTEGER NULL");
    EnsureSqliteColumn(connection, "Products", "CompanyId", "INTEGER NULL");
    EnsureSqliteColumn(connection, "Orders", "CompanyId", "INTEGER NULL");
    EnsureSqliteColumn(connection, "Orders", "AssignedEmployeeUserId", "INTEGER NULL");
    EnsureSqliteColumn(connection, "Transactions", "CompanyId", "INTEGER NULL");
    EnsureSqliteColumn(connection, "AuditLogs", "CorrelationId", "TEXT NULL");
    EnsureOrderPaymentsTable(connection);
    EnsureOrderInvoicesTable(connection);
    EnsureOrderHistoryTable(connection);
    EnsureCustomerFavoritesTable(connection);
    EnsureMarketProviderConfigurationsTable(connection);
    EnsureMarketQuoteSnapshotsTable(connection);
    EnsureSqliteColumn(connection, "MarketQuoteSnapshots", "SourceType", "TEXT NOT NULL DEFAULT 'live_market'");
    EnsureSqliteColumn(connection, "MarketQuoteSnapshots", "CalculationBasis", "TEXT NULL");
    EnsureSqliteColumn(connection, "MarketQuoteSnapshots", "DataQualityStatus", "TEXT NOT NULL DEFAULT 'ok'");
    EnsureSqliteColumn(connection, "MarketQuoteSnapshots", "QualityWarningsJson", "TEXT NOT NULL DEFAULT '[]'");
    EnsureMarketWatchlistItemsTable(connection);
    EnsureWorkTasksTable(connection);
    EnsureWorkTaskHistoryEntriesTable(connection);
    EnsureAccountVerificationTokensTable(connection);
    EnsureRefreshTokensTable(connection);

    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE UNIQUE INDEX IF NOT EXISTS IX_AppUsers_Email ON AppUsers (Email);
        CREATE UNIQUE INDEX IF NOT EXISTS IX_AppUsers_Phone ON AppUsers (Phone);
    """;
    command.ExecuteNonQuery();
}

static void EnsureSqliteColumn(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
{
    using var pragma = connection.CreateCommand();
    pragma.CommandText = $"PRAGMA table_info('{tableName}');";

    var exists = false;
    using (var reader = pragma.ExecuteReader())
    {
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }
    }

    if (exists)
    {
        return;
    }

    using var alter = connection.CreateCommand();
    alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
    alter.ExecuteNonQuery();
}

static void EnsureCompaniesTable(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS Companies (
            Id INTEGER NOT NULL CONSTRAINT PK_Companies PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Code TEXT NULL,
            Address TEXT NULL,
            ContactEmail TEXT NULL,
            ContactPhone TEXT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_Companies_Name ON Companies (Name);
    """;
    command.ExecuteNonQuery();
}

static void EnsureOrderPaymentsTable(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS OrderPayments (
            Id INTEGER NOT NULL CONSTRAINT PK_OrderPayments PRIMARY KEY AUTOINCREMENT,
            OrderId INTEGER NOT NULL,
            Method INTEGER NOT NULL,
            Status INTEGER NOT NULL,
            Amount TEXT NOT NULL,
            ReferenceNumber TEXT NULL,
            Notes TEXT NULL,
            PaidAt TEXT NULL,
            CreatedAt TEXT NOT NULL,
            CreatedByUsername TEXT NULL,
            CONSTRAINT FK_OrderPayments_Orders_OrderId FOREIGN KEY (OrderId) REFERENCES Orders (Id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS IX_OrderPayments_OrderId ON OrderPayments (OrderId);
        CREATE INDEX IF NOT EXISTS IX_OrderPayments_CreatedAt ON OrderPayments (CreatedAt);
        ALTER TABLE Orders ADD COLUMN PaidAmount TEXT NOT NULL DEFAULT '0';
    """;

    try
    {
        command.ExecuteNonQuery();
    }
    catch
    {
        EnsureSqliteColumn(connection, "Orders", "PaidAmount", "TEXT NOT NULL DEFAULT '0'");
    }

    EnsureSqliteColumn(connection, "Orders", "PaymentStatus", "INTEGER NOT NULL DEFAULT 1");
    EnsureSqliteColumn(connection, "Orders", "PreferredPaymentMethod", "INTEGER NULL");
    EnsureSqliteColumn(connection, "OrderPayments", "ProviderKey", "TEXT NULL");
    EnsureSqliteColumn(connection, "OrderPayments", "ProviderTransactionId", "TEXT NULL");
    EnsureSqliteColumn(connection, "OrderPayments", "IdempotencyKey", "TEXT NULL");
    EnsureSqliteColumn(connection, "OrderPayments", "InstallmentCount", "INTEGER NOT NULL DEFAULT 1");
    EnsureSqliteColumn(connection, "OrderPayments", "RequiresThreeDSecure", "INTEGER NOT NULL DEFAULT 0");
    EnsureSqliteColumn(connection, "OrderPayments", "ThreeDSecureStatus", "TEXT NULL");
    EnsureSqliteColumn(connection, "OrderPayments", "ParentPaymentId", "INTEGER NULL");
    EnsureSqliteColumn(connection, "OrderPayments", "IsRefund", "INTEGER NOT NULL DEFAULT 0");
    EnsureSqliteColumn(connection, "OrderPayments", "IsPartialRefund", "INTEGER NOT NULL DEFAULT 0");
    EnsureSqliteColumn(connection, "OrderPayments", "FailureCode", "TEXT NULL");
    EnsureSqliteColumn(connection, "OrderPayments", "FailureMessage", "TEXT NULL");

    using var indexCommand = connection.CreateCommand();
    indexCommand.CommandText = """
        CREATE UNIQUE INDEX IF NOT EXISTS IX_OrderPayments_IdempotencyKey ON OrderPayments (IdempotencyKey);
        CREATE UNIQUE INDEX IF NOT EXISTS IX_OrderPayments_ProviderKey_ProviderTransactionId ON OrderPayments (ProviderKey, ProviderTransactionId);
    """;
    indexCommand.ExecuteNonQuery();
}

static void EnsureOrderInvoicesTable(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS OrderInvoices (
            Id INTEGER NOT NULL CONSTRAINT PK_OrderInvoices PRIMARY KEY AUTOINCREMENT,
            OrderId INTEGER NOT NULL,
            InvoiceNumber TEXT NOT NULL,
            FilePath TEXT NOT NULL,
            FileName TEXT NOT NULL,
            TotalAmount TEXT NOT NULL,
            PaidAmount TEXT NOT NULL,
            InvoiceDate TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            CONSTRAINT FK_OrderInvoices_Orders_OrderId FOREIGN KEY (OrderId) REFERENCES Orders (Id) ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_OrderInvoices_InvoiceNumber ON OrderInvoices (InvoiceNumber);
        CREATE INDEX IF NOT EXISTS IX_OrderInvoices_OrderId ON OrderInvoices (OrderId);
        CREATE INDEX IF NOT EXISTS IX_OrderInvoices_InvoiceDate ON OrderInvoices (InvoiceDate);
    """;
    command.ExecuteNonQuery();
}

static void EnsureOrderHistoryTable(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS OrderHistoryEntries (
            Id INTEGER NOT NULL CONSTRAINT PK_OrderHistoryEntries PRIMARY KEY AUTOINCREMENT,
            OrderId INTEGER NOT NULL,
            Type INTEGER NOT NULL,
            Title TEXT NOT NULL,
            Description TEXT NOT NULL,
            ActorUsername TEXT NULL,
            ActorRole TEXT NULL,
            RelatedEntityName TEXT NULL,
            RelatedEntityId TEXT NULL,
            MetadataJson TEXT NULL,
            CreatedAt TEXT NOT NULL,
            CONSTRAINT FK_OrderHistoryEntries_Orders_OrderId FOREIGN KEY (OrderId) REFERENCES Orders (Id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS IX_OrderHistoryEntries_OrderId ON OrderHistoryEntries (OrderId);
        CREATE INDEX IF NOT EXISTS IX_OrderHistoryEntries_CreatedAt ON OrderHistoryEntries (CreatedAt);
    """;
    command.ExecuteNonQuery();
}

static void EnsureCustomerFavoritesTable(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS CustomerFavorites (
            Id INTEGER NOT NULL CONSTRAINT PK_CustomerFavorites PRIMARY KEY AUTOINCREMENT,
            CustomerId INTEGER NOT NULL,
            ProductId INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL,
            CONSTRAINT FK_CustomerFavorites_Customers_CustomerId FOREIGN KEY (CustomerId) REFERENCES Customers (Id) ON DELETE CASCADE,
            CONSTRAINT FK_CustomerFavorites_Products_ProductId FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_CustomerFavorites_CustomerId_ProductId ON CustomerFavorites (CustomerId, ProductId);
        CREATE INDEX IF NOT EXISTS IX_CustomerFavorites_CreatedAt ON CustomerFavorites (CreatedAt);
        CREATE INDEX IF NOT EXISTS IX_CustomerFavorites_ProductId ON CustomerFavorites (ProductId);
    """;
    command.ExecuteNonQuery();
}

static void EnsureMarketProviderConfigurationsTable(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS MarketProviderConfigurations (
            Id INTEGER NOT NULL CONSTRAINT PK_MarketProviderConfigurations PRIMARY KEY AUTOINCREMENT,
            ProviderKey TEXT NOT NULL,
            DisplayName TEXT NOT NULL,
            IsEnabled INTEGER NOT NULL,
            SupportsRealtime INTEGER NOT NULL,
            Priority INTEGER NOT NULL,
            RefreshIntervalSeconds INTEGER NOT NULL,
            BaseUrl TEXT NULL,
            ApiKey TEXT NULL,
            LastSuccessfulSyncAt TEXT NULL,
            LastFailureAt TEXT NULL,
            LastError TEXT NULL,
            FailureCount INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_MarketProviderConfigurations_ProviderKey ON MarketProviderConfigurations (ProviderKey);
        CREATE INDEX IF NOT EXISTS IX_MarketProviderConfigurations_IsEnabled_Priority ON MarketProviderConfigurations (IsEnabled, Priority);
    """;
    command.ExecuteNonQuery();
}

static void EnsureMarketQuoteSnapshotsTable(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS MarketQuoteSnapshots (
            Id INTEGER NOT NULL CONSTRAINT PK_MarketQuoteSnapshots PRIMARY KEY AUTOINCREMENT,
            Symbol TEXT NOT NULL,
            DisplayName TEXT NOT NULL,
            Category INTEGER NOT NULL,
            UnitLabel TEXT NOT NULL,
            NativeCurrency TEXT NOT NULL,
            PriceInUsd TEXT NOT NULL,
            Price24hAgoInUsd TEXT NOT NULL,
            High24hInUsd TEXT NOT NULL,
            Low24hInUsd TEXT NOT NULL,
            SparklineJson TEXT NOT NULL,
            ProviderKey TEXT NOT NULL,
            ProviderDisplayName TEXT NOT NULL,
            Note TEXT NULL,
            SourceType TEXT NOT NULL DEFAULT 'live_market',
            CalculationBasis TEXT NULL,
            DataQualityStatus TEXT NOT NULL DEFAULT 'ok',
            QualityWarningsJson TEXT NOT NULL DEFAULT '[]',
            IsFallback INTEGER NOT NULL,
            SortOrder INTEGER NOT NULL,
            LastUpdatedAt TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_MarketQuoteSnapshots_Symbol ON MarketQuoteSnapshots (Symbol);
        CREATE INDEX IF NOT EXISTS IX_MarketQuoteSnapshots_Category ON MarketQuoteSnapshots (Category);
        CREATE INDEX IF NOT EXISTS IX_MarketQuoteSnapshots_LastUpdatedAt ON MarketQuoteSnapshots (LastUpdatedAt);
    """;
    command.ExecuteNonQuery();
}

static void EnsureMarketWatchlistItemsTable(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS MarketWatchlistItems (
            Id INTEGER NOT NULL CONSTRAINT PK_MarketWatchlistItems PRIMARY KEY AUTOINCREMENT,
            AppUserId INTEGER NOT NULL,
            Symbol TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            CONSTRAINT FK_MarketWatchlistItems_AppUsers_AppUserId FOREIGN KEY (AppUserId) REFERENCES AppUsers (Id) ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_MarketWatchlistItems_AppUserId_Symbol ON MarketWatchlistItems (AppUserId, Symbol);
        CREATE INDEX IF NOT EXISTS IX_MarketWatchlistItems_CreatedAt ON MarketWatchlistItems (CreatedAt);
    """;
    command.ExecuteNonQuery();
}

static void EnsureWorkTasksTable(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS WorkTasks (
            Id INTEGER NOT NULL CONSTRAINT PK_WorkTasks PRIMARY KEY AUTOINCREMENT,
            CompanyId INTEGER NOT NULL,
            Title TEXT NOT NULL,
            Description TEXT NULL,
            Priority INTEGER NOT NULL,
            Status INTEGER NOT NULL,
            DueDate TEXT NULL,
            AssignedToUserId INTEGER NOT NULL,
            AssignedByUserId INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            CompletedAt TEXT NULL,
            CONSTRAINT FK_WorkTasks_Companies_CompanyId FOREIGN KEY (CompanyId) REFERENCES Companies (Id) ON DELETE CASCADE,
            CONSTRAINT FK_WorkTasks_AppUsers_AssignedToUserId FOREIGN KEY (AssignedToUserId) REFERENCES AppUsers (Id) ON DELETE RESTRICT,
            CONSTRAINT FK_WorkTasks_AppUsers_AssignedByUserId FOREIGN KEY (AssignedByUserId) REFERENCES AppUsers (Id) ON DELETE RESTRICT
        );
        CREATE INDEX IF NOT EXISTS IX_WorkTasks_CompanyId_Status_DueDate ON WorkTasks (CompanyId, Status, DueDate);
    """;
    command.ExecuteNonQuery();
}

static void EnsureWorkTaskHistoryEntriesTable(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS WorkTaskHistoryEntries (
            Id INTEGER NOT NULL CONSTRAINT PK_WorkTaskHistoryEntries PRIMARY KEY AUTOINCREMENT,
            WorkTaskId INTEGER NOT NULL,
            ActionTitle TEXT NOT NULL,
            Description TEXT NULL,
            PreviousStatus INTEGER NULL,
            NewStatus INTEGER NOT NULL,
            ActorUserId INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL,
            CONSTRAINT FK_WorkTaskHistoryEntries_WorkTasks_WorkTaskId FOREIGN KEY (WorkTaskId) REFERENCES WorkTasks (Id) ON DELETE CASCADE,
            CONSTRAINT FK_WorkTaskHistoryEntries_AppUsers_ActorUserId FOREIGN KEY (ActorUserId) REFERENCES AppUsers (Id) ON DELETE RESTRICT
        );
        CREATE INDEX IF NOT EXISTS IX_WorkTaskHistoryEntries_WorkTaskId_CreatedAt ON WorkTaskHistoryEntries (WorkTaskId, CreatedAt);
    """;
    command.ExecuteNonQuery();
}

static void EnsureAccountVerificationTokensTable(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS AccountVerificationTokens (
            Id INTEGER NOT NULL CONSTRAINT PK_AccountVerificationTokens PRIMARY KEY AUTOINCREMENT,
            AppUserId INTEGER NOT NULL,
            Purpose TEXT NOT NULL,
            Channel TEXT NOT NULL,
            Destination TEXT NOT NULL,
            TokenHash TEXT NOT NULL,
            Attempts INTEGER NOT NULL DEFAULT 0,
            SendCount INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL,
            LastSentAt TEXT NOT NULL,
            ExpiresAt TEXT NOT NULL,
            ConsumedAt TEXT NULL,
            RequestIp TEXT NULL,
            CONSTRAINT FK_AccountVerificationTokens_AppUsers_AppUserId FOREIGN KEY (AppUserId) REFERENCES AppUsers (Id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS IX_AccountVerificationTokens_User_Purpose_Channel_Consumed ON AccountVerificationTokens (AppUserId, Purpose, Channel, ConsumedAt);
        CREATE INDEX IF NOT EXISTS IX_AccountVerificationTokens_ExpiresAt ON AccountVerificationTokens (ExpiresAt);
    """;
    command.ExecuteNonQuery();
}

static void EnsureRefreshTokensTable(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS RefreshTokens (
            Id INTEGER NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY AUTOINCREMENT,
            AppUserId INTEGER NOT NULL,
            TokenHash TEXT NOT NULL,
            SecurityStampSnapshot TEXT NOT NULL,
            DeviceName TEXT NULL,
            CreatedByIp TEXT NULL,
            CreatedByUserAgent TEXT NULL,
            CreatedAt TEXT NOT NULL,
            ExpiresAt TEXT NOT NULL,
            RevokedAt TEXT NULL,
            RevokedByIp TEXT NULL,
            ReplacedByTokenHash TEXT NULL,
            CONSTRAINT FK_RefreshTokens_AppUsers_AppUserId FOREIGN KEY (AppUserId) REFERENCES AppUsers (Id) ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_RefreshTokens_TokenHash ON RefreshTokens (TokenHash);
        CREATE INDEX IF NOT EXISTS IX_RefreshTokens_AppUserId_ExpiresAt_RevokedAt ON RefreshTokens (AppUserId, ExpiresAt, RevokedAt);
    """;
    command.ExecuteNonQuery();
}

static void SeedUsersAndAccounts(AppDbContext db, IPasswordHasher<AppUser> passwordHasher)
{
    // Demo hesap seeding kapatildi. Uretim bootstrap hesaplari SeedProductionEssentials icinde yonetilir.
}

static int EnsureCompany(AppDbContext db, string name, string code, string email, string phone, string address)
{
    var normalizedCode = code.Trim().ToUpperInvariant();
    var company = db.Companies.FirstOrDefault(x => x.Code == normalizedCode || x.Name == name);
    if (company is null)
    {
        company = new Company
        {
            Name = name,
            Code = normalizedCode,
            ContactEmail = email.Trim().ToLowerInvariant(),
            ContactPhone = NormalizePhone(phone),
            Address = address,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Companies.Add(company);
        db.SaveChanges();
    }
    else
    {
        company.Name = name;
        company.Code = normalizedCode;
        company.ContactEmail = email.Trim().ToLowerInvariant();
        company.ContactPhone = NormalizePhone(phone);
        company.Address = address;
        company.IsActive = true;
        db.SaveChanges();
    }

    return company.Id;
}

static void SeedMarketProviders(AppDbContext db)
{
    var liveProvider = db.MarketProviderConfigurations.FirstOrDefault(x => x.ProviderKey == "live-api");
    if (liveProvider is null)
    {
        db.MarketProviderConfigurations.Add(new MarketProviderConfiguration
        {
            ProviderKey = "live-api",
            DisplayName = "Yahoo Finance + TCMB",
            IsEnabled = true,
            SupportsRealtime = true,
            Priority = 1,
            RefreshIntervalSeconds = 45,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }
    else
    {
        liveProvider.DisplayName = "Yahoo Finance + TCMB";
        liveProvider.IsEnabled = true;
        liveProvider.SupportsRealtime = true;
        liveProvider.Priority = 1;
        liveProvider.RefreshIntervalSeconds = 45;
        liveProvider.UpdatedAt = DateTime.UtcNow;
    }

    var fallbackProvider = db.MarketProviderConfigurations.FirstOrDefault(x => x.ProviderKey == "fallback-core");
    if (fallbackProvider is not null)
    {
        fallbackProvider.DisplayName = "Legacy Mock Provider";
        fallbackProvider.IsEnabled = false;
        fallbackProvider.SupportsRealtime = false;
        fallbackProvider.Priority = 9;
        fallbackProvider.UpdatedAt = DateTime.UtcNow;
    }

    db.SaveChanges();
}

static void EnsureProductionBootstrapAccounts(
    AppDbContext db,
    IPasswordHasher<AppUser> passwordHasher,
    IConfiguration configuration)
{
    var adminSection = configuration.GetSection("BootstrapAdmin");
    var managerSection = configuration.GetSection("BootstrapManager");
    var company = db.Companies.First(x => x.Code == "MGOLD");

    var systemAdmin = EnsureBootstrapInternalUser(
        db,
        passwordHasher,
        section: adminSection,
        fallbackUsername: "platform.admin",
        fallbackFullName: "Sistem Yoneticisi",
        fallbackEmail: "sakizciomerbugra@gmail.com",
        fallbackPhone: "+905510840483",
        role: RoleConstants.SystemAdmin,
        companyId: null,
        createdByUserId: null);
    db.SaveChanges();

    EnsureBootstrapInternalUser(
        db,
        passwordHasher,
        section: managerSection,
        fallbackUsername: "firma.yoneticisi",
        fallbackFullName: "Firma Yoneticisi",
        fallbackEmail: "sakizciomerbugra895gmail.com",
        fallbackPhone: "+905510840484",
        role: RoleConstants.Manager,
        companyId: company.Id,
        createdByUserId: systemAdmin.Id == 0 ? null : systemAdmin.Id);

    DisableLegacyDemoAccounts(db);
    db.SaveChanges();
}

static AppUser EnsureBootstrapInternalUser(
    AppDbContext db,
    IPasswordHasher<AppUser> passwordHasher,
    IConfigurationSection section,
    string fallbackUsername,
    string fallbackFullName,
    string fallbackEmail,
    string fallbackPhone,
    string role,
    int? companyId,
    int? createdByUserId)
{
    var username = (section["Username"] ?? fallbackUsername).Trim().ToLowerInvariant();
    var fullName = section["FullName"] ?? fallbackFullName;
    var email = (section["Email"] ?? fallbackEmail).Trim().ToLowerInvariant();
    var phone = NormalizePhone(section["Phone"] ?? fallbackPhone);
    var passwordHash = section["PasswordHash"];
    var plainPasswordFromEnvironment = section["Password"];

    var user = db.AppUsers.FirstOrDefault(x => x.Email == email || x.Username == username);
    if (user is null)
    {
        user = db.AppUsers.FirstOrDefault(x => x.Role == role && x.Email == email)
            ?? new AppUser { CreatedAt = DateTime.UtcNow };
        if (user.Id == 0)
        {
            db.AppUsers.Add(user);
        }
    }

    user.Username = username;
    user.FullName = fullName;
    user.Email = email;
    user.Phone = phone;
    user.Role = role;
    user.CompanyId = companyId;
    user.CustomerId = null;
    user.CreatedByUserId = createdByUserId;
    user.IsActive = true;
    user.EmailConfirmed = true;
    user.EmailConfirmedAt ??= DateTime.UtcNow;
    user.PhoneConfirmed = true;
    user.PhoneConfirmedAt ??= DateTime.UtcNow;
    user.SecurityStamp = string.IsNullOrWhiteSpace(user.SecurityStamp)
        ? Guid.NewGuid().ToString("N")
        : user.SecurityStamp;
    user.PasswordChangedAt ??= DateTime.UtcNow;

    if (!string.IsNullOrWhiteSpace(passwordHash))
    {
        user.PasswordHash = passwordHash.Trim();
    }
    else if (!string.IsNullOrWhiteSpace(plainPasswordFromEnvironment))
    {
        user.PasswordHash = passwordHasher.HashPassword(user, plainPasswordFromEnvironment);
    }
    else if (string.IsNullOrWhiteSpace(user.PasswordHash))
    {
        throw new InvalidOperationException($"{role} bootstrap password hash is not configured.");
    }

    return user;
}

static void DisableLegacyDemoAccounts(AppDbContext db)
{
    var protectedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sakizciomerbugra@gmail.com",
        "sakizciomerbugra895gmail.com"
    };
    var legacyUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "yonetici",
        "personel",
        "musteri"
    };

    foreach (var user in db.AppUsers.Where(x => legacyUsernames.Contains(x.Username) && !protectedEmails.Contains(x.Email)))
    {
        user.IsActive = false;
        user.LockoutEndAt = DateTime.UtcNow.AddYears(100);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
    }
}

static string NormalizePhone(string phone)
{
    var digits = new string(phone.Where(char.IsDigit).ToArray());
    if (digits.StartsWith("90"))
    {
        digits = digits[2..];
    }

    if (digits.StartsWith("0"))
    {
        digits = digits[1..];
    }

    return $"+90{digits}";
}

static void SeedAdditionalDemoData(AppDbContext db)
{
    var demoCompanyId = db.Companies.OrderBy(x => x.Id).Select(x => x.Id).FirstOrDefault();
    if (!db.Customers.Any())
    {
        db.Customers.AddRange(
            new Customer { Name = "Ayse Demir", Phone = "05550000001", Email = "ayse@example.com", CompanyId = demoCompanyId == 0 ? null : demoCompanyId },
            new Customer { Name = "Mehmet Kaya", Phone = "05550000002", Email = "mehmet@example.com", CompanyId = demoCompanyId == 0 ? null : demoCompanyId });
        db.SaveChanges();
    }

    if (!db.Orders.Any())
    {
        var customer = db.Customers.First();
        var product = db.Products.First();
        var order = new Order
        {
            CompanyId = product.CompanyId,
            CustomerId = customer.Id,
            OrderNumber = $"ORD-SEED-{DateTime.UtcNow:yyyyMMdd}",
            Status = OrderStatus.Preparing,
            Notes = "Demo siparis",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TotalAmount = product.SalePrice,
            AssignedEmployeeUserId = db.AppUsers.Where(x => x.Role == RoleConstants.Employee).Select(x => (int?)x.Id).FirstOrDefault()
        };
        order.Items.Add(new OrderItem
        {
            ProductId = product.Id,
            Quantity = 1,
            UnitPrice = product.SalePrice,
            TotalPrice = product.SalePrice
        });
        db.Orders.Add(order);
        db.SaveChanges();
    }

    if (!db.ProductReviews.Any())
    {
        var product = db.Products.First();
        var customer = db.Customers.First();
        db.ProductReviews.Add(new ProductReview
        {
            ProductId = product.Id,
            CustomerId = customer.Id,
            Rating = 5,
            Comment = "Isçiligi cok basarili, tavsiye ederim.",
            Status = ReviewStatus.Approved,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ModeratedAt = DateTime.UtcNow.AddDays(-1)
        });
        db.SaveChanges();
    }

    if (!db.Notifications.Any())
    {
        db.Notifications.Add(new Notification
        {
            Title = "Hos geldiniz",
            Message = "Dashboard ve yeni moduller kullanima hazir.",
            Type = NotificationType.Info,
            TargetRole = RoleConstants.Admin,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    if (!db.WorkTasks.Any())
    {
        var manager = db.AppUsers.FirstOrDefault(x => x.Role == RoleConstants.Manager);
        var employee = db.AppUsers.FirstOrDefault(x => x.Role == RoleConstants.Employee);
        if (manager is not null && employee is not null && manager.CompanyId.HasValue)
        {
            var task = new WorkTask
            {
                CompanyId = manager.CompanyId.Value,
                Title = "Vitrin siparislerini kontrol et",
                Description = "Hazirlanan siparislerin kalite ve teslim bilgisini panelden guncelle.",
                Priority = TaskPriority.High,
                Status = MGold.Domain.Enums.TaskStatus.InProgress,
                DueDate = DateTime.UtcNow.AddDays(1),
                AssignedByUserId = manager.Id,
                AssignedToUserId = employee.Id,
                CreatedAt = DateTime.UtcNow.AddHours(-4),
                UpdatedAt = DateTime.UtcNow.AddHours(-1)
            };
            task.HistoryEntries.Add(new WorkTaskHistoryEntry
            {
                ActionTitle = "Gorev olusturuldu",
                Description = "Demo gorev kaydi",
                NewStatus = MGold.Domain.Enums.TaskStatus.Waiting,
                ActorUserId = manager.Id,
                CreatedAt = DateTime.UtcNow.AddHours(-4)
            });
            task.HistoryEntries.Add(new WorkTaskHistoryEntry
            {
                ActionTitle = "Calisma basladi",
                Description = "Personel gorevi isleme aldi.",
                PreviousStatus = MGold.Domain.Enums.TaskStatus.Waiting,
                NewStatus = MGold.Domain.Enums.TaskStatus.InProgress,
                ActorUserId = employee.Id,
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            });
            db.WorkTasks.Add(task);
            db.SaveChanges();
        }
    }
}

static string ResolveInteractiveLoginPath(PathString path)
{
    if (path.StartsWithSegments("/platform", StringComparison.OrdinalIgnoreCase))
    {
        return "/admin/login";
    }

    if (path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase))
    {
        return "/admin/login";
    }

    if (path.StartsWithSegments("/owner", StringComparison.OrdinalIgnoreCase))
    {
        return "/admin/login";
    }

    if (path.StartsWithSegments("/workspace", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/employee", StringComparison.OrdinalIgnoreCase))
    {
        return "/workspace/login";
    }

    if (path.StartsWithSegments("/home", StringComparison.OrdinalIgnoreCase))
    {
        return "/auth/login";
    }

    return "/auth/login";
}

file sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex? mutex;

    private SingleInstanceGuard(Mutex? mutex, bool acquired)
    {
        this.mutex = mutex;
        Acquired = acquired;
    }

    public bool Acquired { get; }

    public static SingleInstanceGuard TryAcquire(IHostEnvironment environment, bool useSqlite, string? sqliteConnectionString)
    {
        try
        {
            var scopeKey = useSqlite
                ? ResolveSqliteDatabasePath(sqliteConnectionString) ?? "sqlite-default"
                : environment.EnvironmentName;
            var lockName = $"MGold::{environment.ApplicationName}::{environment.EnvironmentName}::{scopeKey}".Replace(Path.DirectorySeparatorChar, '_');
            var mutex = new Mutex(initiallyOwned: true, name: lockName, createdNew: out var createdNew);
            return new SingleInstanceGuard(mutex, createdNew);
        }
        catch (AbandonedMutexException)
        {
            return new SingleInstanceGuard(null, true);
        }
        catch
        {
            // If the host platform cannot create a cross-process mutex, don't block startup.
            return new SingleInstanceGuard(null, true);
        }
    }

    public void Dispose()
    {
        if (mutex is null)
        {
            return;
        }

        try
        {
            mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }
        finally
        {
            mutex.Dispose();
        }
    }

    private static string? ResolveSqliteDatabasePath(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
        {
            return null;
        }

        return Path.GetFullPath(builder.DataSource, Directory.GetCurrentDirectory());
    }
}
