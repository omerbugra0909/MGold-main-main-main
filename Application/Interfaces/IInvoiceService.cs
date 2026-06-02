using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface IInvoiceService
{
    Task<OrderInvoiceDto> GenerateForOrderAsync(int orderId, CancellationToken cancellationToken = default);
    Task<(byte[] Content, string FileName)> GetPdfAsync(int invoiceId, CancellationToken cancellationToken = default);
}
