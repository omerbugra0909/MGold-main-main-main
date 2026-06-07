using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface IReportService
{
    Task<OperationalReportDto> GetOperationalReportAsync(string? rangePreset, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default);
    Task<ReportPdfResultDto> GenerateOperationalReportPdfAsync(string? rangePreset, DateTime? startDate, DateTime? endDate, bool archive, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportArchiveItemDto>> GetArchivedReportsAsync(CancellationToken cancellationToken = default);
    Task<ReportPdfResultDto> GetArchivedReportAsync(string fileName, CancellationToken cancellationToken = default);
    Task<ProfitLossSummaryDto> GetProfitLossSummaryAsync(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default);
}
