using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface IReportService
{
    Task<ProfitLossSummaryDto> GetProfitLossSummaryAsync(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default);
}
