using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Domain.Entities;
using MGold.Infrastructure.Repositories.Interfaces;

namespace MGold.Application.Services;

public class AuditLogService(
    IAuditLogRepository auditLogRepository,
    IAccessControlService accessControlService,
    IUnitOfWork unitOfWork) : IAuditLogService
{
    public async Task WriteAsync(CreateAuditLogDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new AuditLog
        {
            UserId = dto.UserId,
            Username = dto.Username,
            UserRole = dto.UserRole,
            ActionType = dto.ActionType,
            EntityName = dto.EntityName,
            EntityId = dto.EntityId,
            HttpMethod = dto.HttpMethod,
            Path = dto.Path,
            CorrelationId = dto.CorrelationId,
            IsSuccess = dto.IsSuccess,
            StatusCode = dto.StatusCode,
            Timestamp = DateTime.UtcNow,
            BeforeState = dto.BeforeState,
            AfterState = dto.AfterState,
            ErrorMessage = dto.ErrorMessage
        };

        await auditLogRepository.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureAdminOnly();

        return (await auditLogRepository.GetRecentAsync(take, cancellationToken))
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                UserId = x.UserId,
                Username = x.Username,
                UserRole = x.UserRole,
                ActionType = x.ActionType,
                EntityName = x.EntityName,
                EntityId = x.EntityId,
                HttpMethod = x.HttpMethod,
                Path = x.Path,
                CorrelationId = x.CorrelationId,
                IsSuccess = x.IsSuccess,
                StatusCode = x.StatusCode,
                Timestamp = x.Timestamp,
                BeforeState = x.BeforeState,
                AfterState = x.AfterState,
                ErrorMessage = x.ErrorMessage
            })
            .ToList();
    }
}
