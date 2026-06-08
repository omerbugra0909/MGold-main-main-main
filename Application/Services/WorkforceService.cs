using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Domain.Constants;
using MGold.Domain.Entities;
using MGold.Domain.Enums;
using MGold.Infrastructure.Data;
using TaskState = MGold.Domain.Enums.TaskStatus;

namespace MGold.Application.Services;

public class WorkforceService(
    AppDbContext context,
    ICurrentUserService currentUserService,
    IAccessControlService accessControlService,
    IPasswordHasher<AppUser> passwordHasher) : IWorkforceService
{
    public async Task<PlatformDashboardDto> GetPlatformDashboardAsync(CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureSystemAdminOnly();

        var companies = await context.Companies
            .AsNoTracking()
            .Include(x => x.Users)
            .Include(x => x.Customers)
            .Include(x => x.Products)
                .ThenInclude(x => x.Reviews)
            .Include(x => x.Orders)
            .Include(x => x.Tasks)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var users = await context.AppUsers
            .AsNoTracking()
            .Include(x => x.Company)
            .OrderByDescending(x => x.CreatedAt)
            .Take(8)
            .ToListAsync(cancellationToken);

        return new PlatformDashboardDto
        {
            TotalCompanies = companies.Count,
            ActiveCompanies = companies.Count(x => x.IsActive),
            TotalInternalUsers = companies.SelectMany(x => x.Users).Count(x => x.Role != RoleConstants.Customer),
            TotalCustomers = companies.SelectMany(x => x.Customers).Count(),
            Companies = companies.Select(MapCompanySummary).ToList(),
            RecentUsers = users.Select(MapUserInfo).ToList()
        };
    }

    public async Task<CompanyWorkspaceDashboardDto> GetCompanyDashboardAsync(CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureManagerOrSystemAdmin();

        var companyId = await ResolveCompanyIdAsync(cancellationToken);
        var company = await context.Companies.AsNoTracking().FirstAsync(x => x.Id == companyId, cancellationToken);
        var products = await context.Products.AsNoTracking().Where(x => x.CompanyId == companyId).ToListAsync(cancellationToken);
        var orders = await context.Orders.AsNoTracking().Where(x => x.CompanyId == companyId).Include(x => x.AssignedEmployeeUser).ToListAsync(cancellationToken);
        var sales = await context.Transactions.AsNoTracking().Where(x => x.CompanyId == companyId && x.Type == TransactionType.Sell).Include(x => x.Product).ToListAsync(cancellationToken);
        var employees = await context.AppUsers.AsNoTracking().Where(x => x.CompanyId == companyId && x.Role == RoleConstants.Employee).ToListAsync(cancellationToken);
        var tasks = await context.WorkTasks
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Include(x => x.AssignedByUser)
            .Include(x => x.AssignedToUser)
            .Include(x => x.HistoryEntries).ThenInclude(x => x.ActorUser)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return new CompanyWorkspaceDashboardDto
        {
            CompanyName = company.Name,
            TotalRevenue = sales.Sum(x => x.TotalPrice),
            TotalProducts = products.Count,
            OpenOrders = orders.Count(x => x.Status is OrderStatus.Preparing or OrderStatus.Ready),
            CompletedOrders = orders.Count(x => x.Status == OrderStatus.Completed),
            ActiveEmployees = employees.Count(x => x.IsActive),
            PendingTasks = tasks.Count(x => x.Status != TaskState.Completed),
            DailyOrderLoad = BuildOrderSeries(orders, 7, day => day.ToString("dd.MM"), date => date.Date),
            WeeklySalesTrend = BuildAggregatedSalesSeries(sales, 6, start => $"H{System.Globalization.ISOWeek.GetWeekOfYear(start)}", start => start.Date.AddDays(-(int)start.DayOfWeek + 1)),
            MonthlySalesTrend = BuildMonthlySalesSeries(sales, 6),
            YearlySalesTrend = sales.GroupBy(x => x.Date.Year)
                .OrderBy(group => group.Key)
                .Select(group => new DashboardSeriesPointDto { Label = group.Key.ToString(), Value = group.Sum(x => x.TotalPrice) })
                .ToList(),
            ProductPerformance = sales.GroupBy(x => x.Product?.Name ?? "Bilinmeyen")
                .Select(group => new PerformanceBreakdownDto { Label = group.Key, Value = group.Sum(x => x.TotalPrice) })
                .OrderByDescending(x => x.Value)
                .Take(6)
                .ToList(),
            EmployeePerformance = orders.Where(x => x.AssignedEmployeeUser is not null)
                .GroupBy(x => x.AssignedEmployeeUser!.FullName)
                .Select(group => new PerformanceBreakdownDto { Label = group.Key, Value = group.Count() })
                .OrderByDescending(x => x.Value)
                .Take(6)
                .ToList(),
            BusyOrderHours = orders.GroupBy(x => x.CreatedAt.Hour)
                .Select(group => new PerformanceBreakdownDto { Label = $"{group.Key:00}:00", Value = group.Count() })
                .OrderByDescending(x => x.Value)
                .Take(6)
                .ToList(),
            Tasks = tasks.Take(8).Select(MapTaskCard).ToList()
        };
    }

    public async Task<EmployeeWorkspaceDto> GetEmployeeWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureEmployeeWorkspaceAccess();

        var userId = currentUserService.UserId ?? throw new AuthorizationException("Kullanıcı çözülemedi.");
        var user = await context.AppUsers.AsNoTracking().Include(x => x.Company).FirstAsync(x => x.Id == userId, cancellationToken);
        accessControlService.EnsureSameCompany(user.CompanyId);

        var tasks = await context.WorkTasks
            .AsNoTracking()
            .Where(x => x.AssignedToUserId == userId)
            .Include(x => x.AssignedByUser)
            .Include(x => x.AssignedToUser)
            .Include(x => x.HistoryEntries).ThenInclude(x => x.ActorUser)
            .OrderBy(x => x.Status)
            .ThenBy(x => x.DueDate)
            .ToListAsync(cancellationToken);

        var orders = await context.Orders
            .AsNoTracking()
            .Where(x => x.AssignedEmployeeUserId == userId)
            .ToListAsync(cancellationToken);

        return new EmployeeWorkspaceDto
        {
            FullName = user.FullName,
            CompanyName = user.Company?.Name ?? "-",
            AssignedTaskCount = tasks.Count,
            CompletedTaskCount = tasks.Count(x => x.Status == TaskState.Completed),
            OpenOrderCount = orders.Count(x => x.Status != OrderStatus.Completed && x.Status != OrderStatus.Cancelled),
            Tasks = tasks.Select(MapTaskCard).ToList()
        };
    }

    public async Task<CompanySummaryDto> CreateCompanyAsync(CreateCompanyDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureSystemAdminOnly();

        var company = new Company
        {
            Name = dto.Name.Trim(),
            Code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim().ToUpperInvariant(),
            Address = dto.Address?.Trim(),
            City = dto.City?.Trim(),
            District = dto.District?.Trim(),
            Description = dto.Description?.Trim(),
            LogoUrl = dto.LogoUrl?.Trim(),
            CoverImageUrl = dto.CoverImageUrl?.Trim(),
            ContactEmail = dto.ContactEmail?.Trim().ToLowerInvariant(),
            ContactPhone = dto.ContactPhone?.Trim(),
            WebsiteUrl = dto.WebsiteUrl?.Trim(),
            TaxOffice = dto.TaxOffice?.Trim(),
            TaxNumber = dto.TaxNumber?.Trim(),
            SocialLinks = dto.SocialLinks?.Trim(),
            WorkingHours = dto.WorkingHours?.Trim(),
            Categories = dto.Categories?.Trim(),
            SearchKeywords = dto.SearchKeywords?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Companies.Add(company);
        await context.SaveChangesAsync(cancellationToken);
        return MapCompanySummary(company);
    }

    public async Task<CompanySummaryDto> UpdateCompanyProfileAsync(int companyId, UpdateCompanyProfileDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureManagerOrSystemAdmin();
        if (!currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            accessControlService.EnsureSameCompany(companyId);
        }

        var company = await context.Companies
            .Include(x => x.Users)
            .Include(x => x.Customers)
            .Include(x => x.Products).ThenInclude(x => x.Reviews)
            .Include(x => x.Orders)
            .Include(x => x.Tasks)
            .FirstOrDefaultAsync(x => x.Id == companyId, cancellationToken)
            ?? throw new BusinessRuleException("Firma bulunamadı.");

        company.Name = dto.Name.Trim();
        company.Code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim().ToUpperInvariant();
        company.Address = dto.Address?.Trim();
        company.City = dto.City?.Trim();
        company.District = dto.District?.Trim();
        company.Description = dto.Description?.Trim();
        company.LogoUrl = dto.LogoUrl?.Trim();
        company.CoverImageUrl = dto.CoverImageUrl?.Trim();
        company.ContactEmail = dto.ContactEmail?.Trim().ToLowerInvariant();
        company.ContactPhone = dto.ContactPhone?.Trim();
        company.WebsiteUrl = dto.WebsiteUrl?.Trim();
        company.TaxOffice = dto.TaxOffice?.Trim();
        company.TaxNumber = dto.TaxNumber?.Trim();
        company.SocialLinks = dto.SocialLinks?.Trim();
        company.WorkingHours = dto.WorkingHours?.Trim();
        company.Categories = dto.Categories?.Trim();
        company.SearchKeywords = dto.SearchKeywords?.Trim();
        company.IsActive = currentUserService.IsInRole(RoleConstants.SystemAdmin) ? dto.IsActive : company.IsActive;

        await context.SaveChangesAsync(cancellationToken);
        return MapCompanySummary(company);
    }

    public async Task<UserInfoDto> CreateInternalUserAsync(CreateInternalUserDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureManagerOrSystemAdmin();

        var normalizedRole = dto.Role.Trim();
        if (normalizedRole != RoleConstants.Manager && normalizedRole != RoleConstants.Employee)
        {
            throw new BusinessRuleException("Sadece yönetici veya çalışan kullanıcısi oluşturulabilir.");
        }

        if (currentUserService.IsInRole(RoleConstants.Manager))
        {
            if (normalizedRole != RoleConstants.Employee)
            {
                throw new BusinessRuleException("Patron hesapları yalnızca kendi firmasina çalışan ekleyebilir.");
            }

            accessControlService.EnsureSameCompany(dto.CompanyId);
        }

        var company = await context.Companies.FirstOrDefaultAsync(x => x.Id == dto.CompanyId, cancellationToken)
            ?? throw new BusinessRuleException("Firma bulunamadı.");

        var normalizedUsername = dto.Username.Trim().ToLowerInvariant();
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var normalizedPhone = NormalizePhone(dto.Phone);

        if (await context.AppUsers.AnyAsync(x => x.Username == normalizedUsername || x.Email == normalizedEmail, cancellationToken))
        {
            throw new BusinessRuleException("Aynı kullanıcı adi veya e-posta ile kayıtlı başka bir kullanıcı var.");
        }

        var user = new AppUser
        {
            Username = normalizedUsername,
            FullName = dto.FullName.Trim(),
            Email = normalizedEmail,
            Phone = normalizedPhone,
            Role = normalizedRole,
            CompanyId = company.Id,
            CreatedByUserId = currentUserService.UserId,
            IsActive = true,
            EmailConfirmed = true,
            EmailConfirmedAt = DateTime.UtcNow,
            PhoneConfirmed = true,
            PhoneConfirmedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);

        context.AppUsers.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        user.Company = company;
        return MapUserInfo(user);
    }

    public async Task<TaskCardDto> AssignTaskAsync(CreateTaskDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureManagerOrSystemAdmin();

        var assignedByUserId = currentUserService.UserId ?? throw new AuthorizationException("Kullanıcı bulunamadı.");
        var assignedBy = await context.AppUsers.FirstAsync(x => x.Id == assignedByUserId, cancellationToken);
        var assignedTo = await context.AppUsers.FirstOrDefaultAsync(x => x.Id == dto.AssignedToUserId, cancellationToken)
            ?? throw new BusinessRuleException("Görev atanacak çalışan bulunamadı.");

        accessControlService.EnsureSameCompany(assignedBy.CompanyId);
        accessControlService.EnsureSameCompany(assignedTo.CompanyId);

        if (assignedTo.Role != RoleConstants.Employee)
        {
            throw new BusinessRuleException("Görev yalnızca çalışan rolundeki kullanıcılara atanabilir.");
        }

        var task = new WorkTask
        {
            CompanyId = assignedBy.CompanyId ?? assignedTo.CompanyId ?? throw new BusinessRuleException("Firma bağlami bulunamadı."),
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            AssignedByUserId = assignedByUserId,
            AssignedToUserId = assignedTo.Id,
            Priority = dto.Priority,
            Status = TaskState.Waiting,
            DueDate = dto.DueDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        task.HistoryEntries.Add(new WorkTaskHistoryEntry
        {
            ActionTitle = "Görev oluşturuldu",
            Description = "Yönetici tarafından yeni görev atandi.",
            NewStatus = TaskState.Waiting,
            ActorUserId = assignedByUserId,
            CreatedAt = DateTime.UtcNow
        });

        context.WorkTasks.Add(task);
        await context.SaveChangesAsync(cancellationToken);

        return await context.WorkTasks
            .AsNoTracking()
            .Where(x => x.Id == task.Id)
            .Include(x => x.AssignedByUser)
            .Include(x => x.AssignedToUser)
            .Include(x => x.HistoryEntries).ThenInclude(x => x.ActorUser)
            .Select(x => MapTaskCard(x))
            .FirstAsync(cancellationToken);
    }

    public async Task<TaskCardDto> UpdateTaskStatusAsync(int taskId, UpdateTaskStatusDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureEmployeeWorkspaceAccess();

        var userId = currentUserService.UserId ?? throw new AuthorizationException("Kullanıcı bulunamadı.");
        var task = await context.WorkTasks
            .Include(x => x.AssignedByUser)
            .Include(x => x.AssignedToUser)
            .Include(x => x.HistoryEntries)
            .FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken)
            ?? throw new BusinessRuleException("Görev bulunamadı.");

        accessControlService.EnsureSameCompany(task.CompanyId);

        var isManager = currentUserService.IsInRole(RoleConstants.Manager) || currentUserService.IsInRole(RoleConstants.SystemAdmin);
        if (!isManager && task.AssignedToUserId != userId)
        {
            throw new AuthorizationException("Bu görev sadece ilgili çalışan tarafından güncellenebilir.");
        }

        var previousStatus = task.Status;
        task.Status = dto.Status;
        task.UpdatedAt = DateTime.UtcNow;
        task.CompletedAt = dto.Status == TaskState.Completed ? DateTime.UtcNow : null;
        task.HistoryEntries.Add(new WorkTaskHistoryEntry
        {
            WorkTaskId = task.Id,
            ActionTitle = "Durum güncellendi",
            Description = dto.Note?.Trim(),
            PreviousStatus = previousStatus,
            NewStatus = dto.Status,
            ActorUserId = userId,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);

        return await context.WorkTasks
            .AsNoTracking()
            .Where(x => x.Id == task.Id)
            .Include(x => x.AssignedByUser)
            .Include(x => x.AssignedToUser)
            .Include(x => x.HistoryEntries).ThenInclude(x => x.ActorUser)
            .Select(x => MapTaskCard(x))
            .FirstAsync(cancellationToken);
    }

    private async Task<int> ResolveCompanyIdAsync(CancellationToken cancellationToken)
    {
        if (currentUserService.CompanyId is int scopedCompanyId)
        {
            return scopedCompanyId;
        }

        if (currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            var firstCompanyId = await context.Companies
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Id)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (firstCompanyId.HasValue)
            {
                return firstCompanyId.Value;
            }
        }

        throw new AuthorizationException("Firma bağlami bulunamadı.");
    }

    private static CompanySummaryDto MapCompanySummary(Company company)
    {
        var reviews = company.Products
            .SelectMany(x => x.Reviews)
            .Where(x => x.Status == ReviewStatus.Approved)
            .ToList();

        return new CompanySummaryDto
        {
            Id = company.Id,
            Name = company.Name,
            Code = company.Code,
            Description = company.Description,
            LogoUrl = company.LogoUrl,
            CoverImageUrl = company.CoverImageUrl,
            City = company.City,
            District = company.District,
            Categories = company.Categories,
            AverageRating = reviews.Count == 0 ? 0 : Math.Round((decimal)reviews.Average(x => x.Rating), 2),
            ReviewCount = reviews.Count,
            IsActive = company.IsActive,
            ManagerCount = company.Users.Count(x => x.Role == RoleConstants.Manager),
            EmployeeCount = company.Users.Count(x => x.Role == RoleConstants.Employee),
            CustomerCount = company.Customers.Count,
            ProductCount = company.Products.Count,
            OpenOrderCount = company.Orders.Count(x => x.Status is OrderStatus.Preparing or OrderStatus.Ready),
            PendingTaskCount = company.Tasks.Count(x => x.Status != TaskState.Completed)
        };
    }

    private static UserInfoDto MapUserInfo(AppUser user)
        => new()
        {
            Id = user.Id,
            CompanyId = user.CompanyId,
            CompanyName = user.Company?.Name,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            IsActive = user.IsActive,
            CustomerId = user.CustomerId,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };

    private static TaskCardDto MapTaskCard(WorkTask task)
        => new()
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            AssignedTo = task.AssignedToUser.FullName,
            AssignedBy = task.AssignedByUser.FullName,
            Priority = task.Priority,
            Status = task.Status,
            DueDate = task.DueDate,
            CreatedAt = task.CreatedAt,
            History = task.HistoryEntries
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new TaskHistoryDto
                {
                    ActionTitle = x.ActionTitle,
                    Description = x.Description,
                    ActorName = x.ActorUser.FullName,
                    PreviousStatus = x.PreviousStatus,
                    NewStatus = x.NewStatus,
                    CreatedAt = x.CreatedAt
                })
                .ToList()
        };

    private static IReadOnlyList<DashboardSeriesPointDto> BuildOrderSeries(
        IReadOnlyList<Order> orders,
        int days,
        Func<DateTime, string> labelFactory,
        Func<DateTime, DateTime> normalize)
    {
        var start = DateTime.UtcNow.Date.AddDays(-(days - 1));
        return Enumerable.Range(0, days)
            .Select(offset => start.AddDays(offset))
            .Select(day => new DashboardSeriesPointDto
            {
                Label = labelFactory(day),
                Value = orders.Count(order => normalize(order.CreatedAt) == normalize(day))
            })
            .ToList();
    }

    private static IReadOnlyList<DashboardSeriesPointDto> BuildAggregatedSalesSeries(
        IReadOnlyList<Transaction> sales,
        int buckets,
        Func<DateTime, string> labelFactory,
        Func<DateTime, DateTime> bucketStartFactory)
    {
        return sales
            .GroupBy(x => bucketStartFactory(x.Date))
            .OrderByDescending(group => group.Key)
            .Take(buckets)
            .OrderBy(group => group.Key)
            .Select(group => new DashboardSeriesPointDto
            {
                Label = labelFactory(group.Key),
                Value = group.Sum(x => x.TotalPrice)
            })
            .ToList();
    }

    private static IReadOnlyList<DashboardSeriesPointDto> BuildMonthlySalesSeries(IReadOnlyList<Transaction> sales, int months)
    {
        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-(months - 1));
        return Enumerable.Range(0, months)
            .Select(offset => start.AddMonths(offset))
            .Select(month => new DashboardSeriesPointDto
            {
                Label = month.ToString("MMM yy"),
                Value = sales.Where(x => x.Date.Year == month.Year && x.Date.Month == month.Month).Sum(x => x.TotalPrice)
            })
            .ToList();
    }

    private static string NormalizePhone(string phone)
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
}
