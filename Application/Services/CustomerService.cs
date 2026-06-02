using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Application.Mappings;
using MGold.Common;
using MGold.Domain.Constants;
using MGold.Domain.Entities;
using MGold.Infrastructure.Data;
using MGold.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MGold.Application.Services;

public class CustomerService(
    ICustomerRepository customerRepository,
    IUnitOfWork unitOfWork,
    IAccessControlService accessControlService,
    ICurrentUserService currentUserService,
    AppDbContext context) : ICustomerService
{
    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanReadOperationalData();

        var query = context.Customers.AsNoTracking().AsQueryable();
        if (!currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            query = query.Where(x => x.CompanyId == currentUserService.CompanyId);
        }

        return (await query.OrderBy(x => x.Name).ToListAsync(cancellationToken)).Select(x => x.ToDto()).ToList();
    }

    public async Task<CustomerDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanReadOperationalData();

        var customer = await context.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer with id {id} was not found.");
        accessControlService.EnsureSameCompany(customer.CompanyId);

        return customer.ToDto();
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanWriteOperationalData();
        var normalizedName = NormalizeRequired(dto.Name, "Customer name");
        var normalizedPhone = NormalizeRequired(dto.Phone, "Phone");
        var phone = NormalizePhone(normalizedPhone);

        var customer = new Customer
        {
            Name = normalizedName,
            Phone = phone,
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            CompanyId = currentUserService.CompanyId
        };

        await customerRepository.AddAsync(customer, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return customer.ToDto();
    }

    public async Task<CustomerDto> UpdateAsync(int id, UpdateCustomerDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanWriteOperationalData();
        var normalizedName = NormalizeRequired(dto.Name, "Customer name");
        var normalizedPhone = NormalizeRequired(dto.Phone, "Phone");
        var phone = NormalizePhone(normalizedPhone);

        var customer = await customerRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer with id {id} was not found.");
        accessControlService.EnsureSameCompany(customer.CompanyId);

        customer.Name = normalizedName;
        customer.Phone = phone;
        customer.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();

        customerRepository.Update(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return customer.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanDeleteData();

        var customer = await customerRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer with id {id} was not found.");
        accessControlService.EnsureSameCompany(customer.CompanyId);

        customerRepository.Remove(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BusinessRuleException($"{fieldName} is required.");
        }

        return normalized;
    }

    private static string NormalizePhone(string phone)
    {
        if (!TurkishPhoneHelper.TryNormalize(phone, out var normalized))
        {
            throw new BusinessRuleException("Only Turkish mobile phone numbers are supported. Use a 5xx number.");
        }

        return normalized;
    }
}
