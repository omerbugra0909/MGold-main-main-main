using System.Text;
using System.Text.Json;
using System.Globalization;
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
var bindUrl = ResolvePreferredBindUrl(builder.Configuration, args, builder.Environment);
if (!string.IsNullOrWhiteSpace(bindUrl))
{
    // IIS/Plesk owns the public domain binding. This optional setting is only for self-host/dev runs.
    builder.WebHost.UseUrls(bindUrl);
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
        jwtSettings.Key = Environment.GetEnvironmentVariable("MGOLD_JWT_KEY")
            ?? "MGoldEmergencyStartupKey_Configure_AppSettings_To_Replace_2026";
    }
}
if (string.IsNullOrWhiteSpace(jwtSettings.Issuer))
{
    jwtSettings.Issuer = builder.Environment.IsDevelopment() ? "MGold.API.Dev" : "MGold.API";
}

if (string.IsNullOrWhiteSpace(jwtSettings.Audience))
{
    jwtSettings.Audience = builder.Environment.IsDevelopment() ? "MGold.Client.Dev" : "MGold.Client";
}

builder.Services.PostConfigure<JwtSettings>(options =>
{
    if (string.IsNullOrWhiteSpace(options.Key)
        || (!builder.Environment.IsDevelopment() && (options.Key.Contains("CHANGE_THIS", StringComparison.OrdinalIgnoreCase) || options.Key.Length < 32)))
    {
        options.Key = jwtSettings.Key;
    }

    if (string.IsNullOrWhiteSpace(options.Issuer))
    {
        options.Issuer = jwtSettings.Issuer;
    }

    if (string.IsNullOrWhiteSpace(options.Audience))
    {
        options.Audience = jwtSettings.Audience;
    }
});
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
var configuredConnectionString = ResolveConfiguredConnectionString(builder.Configuration, useSqlite);
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSqlite)
    {
        options.UseSqlite(configuredConnectionString);
    }
    else
    {
        options.UseSqlServer(configuredConnectionString);
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
using var singleInstanceGuard = app.Environment.IsDevelopment()
    ? SingleInstanceGuard.TryAcquire(app.Environment, useSqlite, configuredConnectionString)
    : SingleInstanceGuard.Noop();
if (!singleInstanceGuard.Acquired)
{
    app.Logger.LogWarning("Another MGold instance is already running for this environment. The new process will exit without rebinding the port.");
    return;
}

ValidateProductionSecrets(app.Configuration, app.Environment, useSqlite, configuredConnectionString, app.Logger);

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

static string? ResolvePreferredBindUrl(IConfiguration configuration, string[] args, IHostEnvironment environment)
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

    var configured = configuration["App:BindUrl"];
    if (string.IsNullOrWhiteSpace(configured) && environment.IsDevelopment())
    {
        configured = configuration["App:BaseUrl"];
    }

    return string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();
}

static string ResolveConfiguredConnectionString(IConfiguration configuration, bool useSqlite)
{
    if (useSqlite)
    {
        return configuration.GetConnectionString("SqliteConnection") ?? "Data Source=mgold.db";
    }

    var configured = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    if (IsPlaceholderSecret(configured))
    {
        configured = Environment.GetEnvironmentVariable("MGOLD_DEFAULT_CONNECTION")
            ?? Environment.GetEnvironmentVariable("SQLCONNSTR_DefaultConnection")
            ?? Environment.GetEnvironmentVariable("CUSTOMCONNSTR_DefaultConnection")
            ?? configured;
    }

    return configured;
}

static bool IsPlaceholderSecret(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return true;
    }

    return value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
        || value.Contains("CHANGE_THIS", StringComparison.OrdinalIgnoreCase)
        || value.Contains("BURAYA", StringComparison.OrdinalIgnoreCase)
        || value.Contains("SIFRESINI", StringComparison.OrdinalIgnoreCase)
        || value.Contains("\u015eIFRESINI", StringComparison.OrdinalIgnoreCase)
        || value.Contains("\u015e\u0130FRES\u0130N\u0130", StringComparison.OrdinalIgnoreCase)
        || value.Contains("GOOGLE_APP_PASSWORD", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Your_strong_password123", StringComparison.OrdinalIgnoreCase);
}

static void ValidateProductionSecrets(IConfiguration configuration, IHostEnvironment environment, bool useSqlite, string defaultConnection, ILogger logger)
{
    if (environment.IsDevelopment())
    {
        return;
    }

    if (useSqlite)
    {
        logger.LogCritical("Production is configured to use SQLite. Configure App:UseSqlite=false and a SQL Server DefaultConnection.");
    }

    if (IsPlaceholderSecret(defaultConnection))
    {
        logger.LogCritical("Production database connection string is missing or contains placeholder secrets. Database-backed features will fail until DefaultConnection is fixed.");
    }

    var sms = configuration.GetSection(SmsSettings.SectionName).Get<SmsSettings>() ?? new SmsSettings();
    if (sms.Enabled && (string.IsNullOrWhiteSpace(sms.Username)
        || string.IsNullOrWhiteSpace(sms.Password)
        || string.IsNullOrWhiteSpace(sms.Originator)))
    {
        logger.LogCritical("Production SMS settings are incomplete while SMS is enabled.");
    }
}

static void InitializeDatabase(WebApplication app, bool useSqlite, bool autoMigrate)
{
    if (!autoMigrate && app.Environment.IsDevelopment())
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
    catch (Exception ex) when (!app.Environment.IsDevelopment())
    {
        app.Logger.LogWarning(ex, "Production database migration failed during startup. Continuing because the production SQL install script can be applied manually.");
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
            Name = "G\u00fcm\u00fc\u015f Erkek Bileklik",
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
            Name = "Zarif Baget P\u0131rlanta Y\u00fcz\u00fck",
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
            Name = "Vintage Safir Ta\u015fl\u0131 Kolye",
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
        SeedMarketSnapshots(db);

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
    EnsureSqliteColumn(connection, "Companies", "City", "TEXT NULL");
    EnsureSqliteColumn(connection, "Companies", "District", "TEXT NULL");
    EnsureSqliteColumn(connection, "Companies", "Description", "TEXT NULL");
    EnsureSqliteColumn(connection, "Companies", "LogoUrl", "TEXT NULL");
    EnsureSqliteColumn(connection, "Companies", "CoverImageUrl", "TEXT NULL");
    EnsureSqliteColumn(connection, "Companies", "WebsiteUrl", "TEXT NULL");
    EnsureSqliteColumn(connection, "Companies", "TaxOffice", "TEXT NULL");
    EnsureSqliteColumn(connection, "Companies", "TaxNumber", "TEXT NULL");
    EnsureSqliteColumn(connection, "Companies", "SocialLinks", "TEXT NULL");
    EnsureSqliteColumn(connection, "Companies", "WorkingHours", "TEXT NULL");
    EnsureSqliteColumn(connection, "Companies", "Categories", "TEXT NULL");
    EnsureSqliteColumn(connection, "Companies", "SearchKeywords", "TEXT NULL");
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
            City TEXT NULL,
            District TEXT NULL,
            Description TEXT NULL,
            LogoUrl TEXT NULL,
            CoverImageUrl TEXT NULL,
            ContactEmail TEXT NULL,
            ContactPhone TEXT NULL,
            WebsiteUrl TEXT NULL,
            TaxOffice TEXT NULL,
            TaxNumber TEXT NULL,
            SocialLinks TEXT NULL,
            WorkingHours TEXT NULL,
            Categories TEXT NULL,
            SearchKeywords TEXT NULL,
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

static void SeedMarketSnapshots(AppDbContext db)
{
    if (db.MarketQuoteSnapshots.Any())
    {
        return;
    }

    var now = DateTime.UtcNow;
    var seedQuotes = new[]
    {
        new MarketQuoteSnapshot { Symbol = "TRY", DisplayName = "T\u00fcrk Liras\u0131", Category = MarketCategory.Currency, UnitLabel = "1 USD", NativeCurrency = "TRY", PriceInUsd = 0.031m, Price24hAgoInUsd = 0.0311m, High24hInUsd = 0.0312m, Low24hInUsd = 0.0308m, SortOrder = 1 },
        new MarketQuoteSnapshot { Symbol = "USD", DisplayName = "Amerikan Dolar\u0131", Category = MarketCategory.Currency, UnitLabel = "1 USD", NativeCurrency = "USD", PriceInUsd = 1m, Price24hAgoInUsd = 1m, High24hInUsd = 1m, Low24hInUsd = 1m, SortOrder = 2 },
        new MarketQuoteSnapshot { Symbol = "EUR", DisplayName = "Euro", Category = MarketCategory.Currency, UnitLabel = "1 EUR", NativeCurrency = "USD", PriceInUsd = 1.08m, Price24hAgoInUsd = 1.079m, High24hInUsd = 1.083m, Low24hInUsd = 1.074m, SortOrder = 3 },
        new MarketQuoteSnapshot { Symbol = "GBP", DisplayName = "\u0130ngiliz Sterlini", Category = MarketCategory.Currency, UnitLabel = "1 GBP", NativeCurrency = "USD", PriceInUsd = 1.27m, Price24hAgoInUsd = 1.268m, High24hInUsd = 1.276m, Low24hInUsd = 1.263m, SortOrder = 4 },
        new MarketQuoteSnapshot { Symbol = "XAU", DisplayName = "Ons Alt\u0131n", Category = MarketCategory.Gold, UnitLabel = "ons", NativeCurrency = "USD", PriceInUsd = 2350m, Price24hAgoInUsd = 2342m, High24hInUsd = 2362m, Low24hInUsd = 2331m, SortOrder = 10 },
        new MarketQuoteSnapshot { Symbol = "GRAM_ALTIN", DisplayName = "Gram Alt\u0131n", Category = MarketCategory.Gold, UnitLabel = "gr", NativeCurrency = "USD", PriceInUsd = 75.55m, Price24hAgoInUsd = 75.2m, High24hInUsd = 76.1m, Low24hInUsd = 74.8m, SortOrder = 11, SourceType = "derived_formula", CalculationBasis = "Ons Alt\u0131n / 31.1034768" },
        new MarketQuoteSnapshot { Symbol = "CEYREK_ALTIN", DisplayName = "\u00c7eyrek Alt\u0131n", Category = MarketCategory.Gold, UnitLabel = "adet", NativeCurrency = "USD", PriceInUsd = 132.5m, Price24hAgoInUsd = 131.9m, High24hInUsd = 133.4m, Low24hInUsd = 130.8m, SortOrder = 12, SourceType = "derived_formula", CalculationBasis = "Gram Alt\u0131n * 1.754" },
        new MarketQuoteSnapshot { Symbol = "YARIM_ALTIN", DisplayName = "Yar\u0131m Alt\u0131n", Category = MarketCategory.Gold, UnitLabel = "adet", NativeCurrency = "USD", PriceInUsd = 265m, Price24hAgoInUsd = 263.8m, High24hInUsd = 266.8m, Low24hInUsd = 261.6m, SortOrder = 13, SourceType = "derived_formula", CalculationBasis = "\u00c7eyrek Alt\u0131n * 2" },
        new MarketQuoteSnapshot { Symbol = "TAM_ALTIN", DisplayName = "Tam Alt\u0131n", Category = MarketCategory.Gold, UnitLabel = "adet", NativeCurrency = "USD", PriceInUsd = 530m, Price24hAgoInUsd = 527.6m, High24hInUsd = 533.6m, Low24hInUsd = 523.2m, SortOrder = 14, SourceType = "derived_formula", CalculationBasis = "Yar\u0131m Alt\u0131n * 2" },
        new MarketQuoteSnapshot { Symbol = "XAG", DisplayName = "G\u00fcm\u00fc\u015f", Category = MarketCategory.Metal, UnitLabel = "ons", NativeCurrency = "USD", PriceInUsd = 30.5m, Price24hAgoInUsd = 30.2m, High24hInUsd = 30.9m, Low24hInUsd = 29.8m, SortOrder = 20 },
        new MarketQuoteSnapshot { Symbol = "XPT", DisplayName = "Platin", Category = MarketCategory.Metal, UnitLabel = "ons", NativeCurrency = "USD", PriceInUsd = 1010m, Price24hAgoInUsd = 1004m, High24hInUsd = 1022m, Low24hInUsd = 996m, SortOrder = 21 },
        new MarketQuoteSnapshot { Symbol = "BRENT", DisplayName = "Brent Petrol", Category = MarketCategory.Commodity, UnitLabel = "varil", NativeCurrency = "USD", PriceInUsd = 82m, Price24hAgoInUsd = 81.3m, High24hInUsd = 83.1m, Low24hInUsd = 80.7m, SortOrder = 30 },
        new MarketQuoteSnapshot { Symbol = "BTC", DisplayName = "Bitcoin", Category = MarketCategory.Crypto, UnitLabel = "adet", NativeCurrency = "USD", PriceInUsd = 68000m, Price24hAgoInUsd = 67250m, High24hInUsd = 68900m, Low24hInUsd = 66500m, SortOrder = 40 },
        new MarketQuoteSnapshot { Symbol = "ETH", DisplayName = "Ethereum", Category = MarketCategory.Crypto, UnitLabel = "adet", NativeCurrency = "USD", PriceInUsd = 3600m, Price24hAgoInUsd = 3560m, High24hInUsd = 3650m, Low24hInUsd = 3510m, SortOrder = 41 }
    };

    foreach (var quote in seedQuotes)
    {
        quote.ProviderKey = "fallback-core";
        quote.ProviderDisplayName = "MGold G\u00fcvenli Ba\u015flang\u0131\u00e7 Verisi";
        quote.Note = "D\u0131\u015f piyasa servisleri g\u00fcncellenene kadar kullan\u0131lan g\u00fcvenli ba\u015flang\u0131\u00e7 snapshot'\u0131.";
        quote.SourceType = string.IsNullOrWhiteSpace(quote.SourceType) ? "manual_fallback" : quote.SourceType;
        quote.DataQualityStatus = "stale";
        quote.QualityWarningsJson = "[\"G\u00fcvenli ba\u015flang\u0131\u00e7 verisi; canl\u0131 kaynak g\u00fcncellenince de\u011fi\u015fir.\"]";
        quote.SparklineJson = JsonSerializer.Serialize(new[] { quote.Price24hAgoInUsd, quote.Low24hInUsd, quote.PriceInUsd, quote.High24hInUsd }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        quote.IsFallback = true;
        quote.CreatedAt = now;
        quote.LastUpdatedAt = now;
    }

    db.MarketQuoteSnapshots.AddRange(seedQuotes);
    if (!db.GoldPrices.Any(x => x.IsActive))
    {
        db.GoldPrices.Add(new GoldPrice
        {
            PricePerGram = Math.Round(seedQuotes.First(x => x.Symbol == "GRAM_ALTIN").PriceInUsd / seedQuotes.First(x => x.Symbol == "TRY").PriceInUsd, 2),
            EffectiveFrom = now,
            Source = "MGold g\u00fcvenli ba\u015flang\u0131\u00e7 verisi",
            IsActive = true,
            CreatedAt = now
        });
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
        fallbackFullName: "Sistem Y\u00f6neticisi",
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
        fallbackFullName: "Firma Y\u00f6neticisi",
        fallbackEmail: "sakizciomerbugra895@gmail.com",
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
    var usernameAliases = BuildBootstrapAliases(username, fallbackUsername, section["Username"]);
    if (role == RoleConstants.Manager)
    {
        usernameAliases.Add("firma.y\u00f6neticisi");
    }

    var emailAliases = BuildBootstrapEmailAliases(email, fallbackEmail, section["Email"]);
    var phoneAliases = BuildBootstrapAliases(phone, fallbackPhone, section["Phone"]);
    var candidates = db.AppUsers
        .Where(x => usernameAliases.Contains(x.Username)
            || emailAliases.Contains(x.Email)
            || phoneAliases.Contains(x.Phone))
        .ToList();

    var user = candidates
        .OrderByDescending(x => string.Equals(x.Role, role, StringComparison.Ordinal))
        .ThenByDescending(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase))
        .ThenByDescending(x => string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase))
        .ThenByDescending(x => string.Equals(x.Phone, phone, StringComparison.OrdinalIgnoreCase))
        .FirstOrDefault();
    if (user is null)
    {
        user = new AppUser { CreatedAt = DateTime.UtcNow };
        db.AppUsers.Add(user);
    }

    foreach (var duplicate in candidates.Where(x => !ReferenceEquals(x, user)))
    {
        duplicate.IsActive = false;
        duplicate.LockoutEndAt = DateTime.UtcNow.AddYears(100);
        duplicate.SecurityStamp = Guid.NewGuid().ToString("N");

        if (string.Equals(duplicate.Username, username, StringComparison.OrdinalIgnoreCase)
            || usernameAliases.Contains(duplicate.Username))
        {
            duplicate.Username = TruncateBootstrapValue($"disabled.{duplicate.Id}.{duplicate.Username}", 80);
        }

        if (string.Equals(duplicate.Email, email, StringComparison.OrdinalIgnoreCase)
            || emailAliases.Contains(duplicate.Email))
        {
            duplicate.Email = TruncateBootstrapValue($"disabled.{duplicate.Id}.{duplicate.Email}", 150);
        }

        if (string.Equals(duplicate.Phone, phone, StringComparison.OrdinalIgnoreCase)
            || phoneAliases.Contains(duplicate.Phone))
        {
            duplicate.Phone = $"+909{duplicate.Id.ToString().PadLeft(9, '0')[..9]}";
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

static HashSet<string> BuildBootstrapAliases(params string?[] values)
{
    var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var value in values)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            aliases.Add(value.Trim().ToLowerInvariant());
        }
    }

    return aliases;
}

static HashSet<string> BuildBootstrapEmailAliases(params string?[] values)
{
    var aliases = BuildBootstrapAliases(values);
    if (aliases.Contains("sakizciomerbugra895gmail.com"))
    {
        aliases.Add("sakizciomerbugra895@gmail.com");
    }

    if (aliases.Contains("sakizciomerbugra895@gmail.com"))
    {
        aliases.Add("sakizciomerbugra895gmail.com");
    }

    return aliases;
}

static string TruncateBootstrapValue(string value, int maxLength)
    => value.Length <= maxLength ? value : value[..maxLength];
static void DisableLegacyDemoAccounts(AppDbContext db)
{
    var protectedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sakizciomerbugra@gmail.com",
        "sakizciomerbugra895@gmail.com",
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
            Notes = "Demo sipari\u015f",
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
            Comment = "\u0130\u015f\u00e7ili\u011fi \u00e7ok ba\u015far\u0131l\u0131, tavsiye ederim.",
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
            Title = "Ho\u015f geldiniz",
            Message = "Dashboard ve yeni mod\u00fcller kullan\u0131ma haz\u0131r.",
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
                Title = "Vitrin sipari\u015flerini kontrol et",
                Description = "Haz\u0131rlanan sipari\u015flerin kalite ve teslim bilgisini panelden g\u00fcncelle.",
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
                ActionTitle = "G\u00f6rev olu\u015fturuldu",
                Description = "Demo g\u00f6rev kayd\u0131",
                NewStatus = MGold.Domain.Enums.TaskStatus.Waiting,
                ActorUserId = manager.Id,
                CreatedAt = DateTime.UtcNow.AddHours(-4)
            });
            task.HistoryEntries.Add(new WorkTaskHistoryEntry
            {
                ActionTitle = "\u00c7al\u0131\u015fma ba\u015flad\u0131",
                Description = "Personel g\u00f6revi i\u015fleme ald\u0131.",
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

    public static SingleInstanceGuard Noop()
        => new(null, true);

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
