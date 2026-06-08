using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Domain.Constants;
using MGold.Domain.Entities;
using MGold.Domain.Enums;
using MGold.Infrastructure.Data;

namespace MGold.Application.Services;

public class InvoiceService(
    AppDbContext context,
    IWebHostEnvironment environment,
    IOptions<CompanyProfileSettings> companyOptions,
    IAccessControlService accessControlService,
    ICurrentUserService currentUserService,
    IOrderHistoryService orderHistoryService) : IInvoiceService
{
    private readonly CompanyProfileSettings _company = companyOptions.Value;

    public async Task<OrderInvoiceDto> GenerateForOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanWriteOperationalData();

        var order = await context.Orders
            .Include(x => x.Customer)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .Include(x => x.Payments)
            .Include(x => x.Invoices)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order with id {orderId} was not found.");
        accessControlService.EnsureSameCompany(order.CompanyId);

        if (order.Invoices.Count > 0)
        {
            var latest = order.Invoices.OrderByDescending(x => x.CreatedAt).First();
            return ToDto(latest);
        }

        if (order.Status != OrderStatus.Completed)
        {
            throw new BusinessRuleException("Invoice can only be generated for completed orders.");
        }

        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}-{order.Id}";
        var pdf = BuildInvoicePdf(order, invoiceNumber);

        var invoiceDirectory = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "generated", "invoices");
        Directory.CreateDirectory(invoiceDirectory);

        var fileName = $"{invoiceNumber}.pdf";
        var filePath = Path.Combine(invoiceDirectory, fileName);
        await File.WriteAllBytesAsync(filePath, pdf, cancellationToken);

        var entity = new OrderInvoice
        {
            OrderId = order.Id,
            InvoiceNumber = invoiceNumber,
            FileName = fileName,
            FilePath = filePath,
            TotalAmount = order.TotalAmount,
            PaidAmount = order.PaidAmount,
            InvoiceDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await context.OrderInvoices.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await orderHistoryService.RecordAsync(
            order.Id,
            OrderHistoryType.InvoiceGenerated,
            "Fatura oluşturuldu",
            $"{invoiceNumber} numaralı dijital fatura oluşturuldu ve arsive kaydedildi.",
            nameof(OrderInvoice),
            entity.Id.ToString(CultureInfo.InvariantCulture),
            cancellationToken: cancellationToken);

        return ToDto(entity);
    }

    public async Task<(byte[] Content, string FileName)> GetPdfAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await context.OrderInvoices
            .AsNoTracking()
            .Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invoice with id {invoiceId} was not found.");
        await EnsureCanReadInvoiceAsync(invoice, cancellationToken);

        if (!File.Exists(invoice.FilePath))
        {
            throw new FileNotFoundException("Invoice PDF file was not found on disk.", invoice.FilePath);
        }

        return (await File.ReadAllBytesAsync(invoice.FilePath, cancellationToken), invoice.FileName);
    }

    private async Task EnsureCanReadInvoiceAsync(OrderInvoice invoice, CancellationToken cancellationToken)
    {
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            return;
        }

        if (currentUserService.IsInRole(RoleConstants.Manager))
        {
            accessControlService.EnsureSameCompany(invoice.Order?.CompanyId);
            return;
        }

        if (currentUserService.IsInRole(RoleConstants.Customer) && currentUserService.UserId is int userId)
        {
            var customerId = await context.AppUsers
                .AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => x.CustomerId)
                .FirstOrDefaultAsync(cancellationToken);

            if (customerId.HasValue && invoice.Order?.CustomerId == customerId.Value)
            {
                return;
            }
        }

        throw new AuthorizationException("Bu faturaya erişim yetkiniz bulunmuyor.");
    }

    private byte[] BuildInvoicePdf(Order order, string invoiceNumber)
    {
        var lines = new List<PdfTextLine>
        {
            new(20, 50, 805, _company.Name),
            new(11, 50, 787, $"{_company.Address} | {_company.Phone} | {_company.Email}"),
            new(11, 50, 771, $"Vergi Dairesi: {_company.TaxOffice} | Vergi No: {_company.TaxNumber}"),
            new(18, 400, 805, "DIJITAL FATURA"),
            new(11, 400, 787, $"Fatura No: {invoiceNumber}"),
            new(11, 400, 771, $"Sipariş No: {order.OrderNumber}"),
            new(11, 400, 755, $"Tarih: {DateTime.UtcNow.ToLocalTime():dd.MM.yyyy HH:mm}"),
            new(13, 50, 725, "MUSTERI BILGILERI"),
            new(11, 50, 707, $"Ad Soyad: {order.Customer.Name}"),
            new(11, 50, 691, $"Telefon: {order.Customer.Phone}"),
            new(11, 50, 675, $"E-Posta: {order.Customer.Email ?? "-"}"),
            new(13, 50, 645, "URUNLER"),
            new(10, 50, 627, "Ürün"),
            new(10, 310, 627, "Adet"),
            new(10, 390, 627, "Birim"),
            new(10, 480, 627, "Toplam")
        };

        var currentY = 607;
        foreach (var item in order.Items)
        {
            lines.Add(new PdfTextLine(10, 50, currentY, item.Product?.Name ?? $"Ürün #{item.ProductId}"));
            lines.Add(new PdfTextLine(10, 310, currentY, item.Quantity.ToString(CultureInfo.InvariantCulture)));
            lines.Add(new PdfTextLine(10, 390, currentY, $"{item.UnitPrice:N2} TL"));
            lines.Add(new PdfTextLine(10, 480, currentY, $"{item.TotalPrice:N2} TL"));
            currentY -= 18;
        }

        var method = order.PreferredPaymentMethod?.ToString() ?? "Belirtilmedi";
        lines.AddRange(
        [
            new PdfTextLine(13, 50, currentY - 16, "ODEME OZETI"),
            new PdfTextLine(11, 50, currentY - 34, $"Ödeme Tipi: {method}"),
            new PdfTextLine(11, 50, currentY - 50, $"Ödeme Durumu: {order.PaymentStatus}"),
            new PdfTextLine(11, 50, currentY - 66, $"Odenen: {order.PaidAmount:N2} TL"),
            new PdfTextLine(11, 50, currentY - 82, $"Kalan: {Math.Max(order.TotalAmount - order.PaidAmount, 0m):N2} TL"),
            new PdfTextLine(13, 400, currentY - 34, "GENEL TOPLAM"),
            new PdfTextLine(16, 400, currentY - 58, $"{order.TotalAmount:N2} TL"),
            new PdfTextLine(10, 50, 80, "Bu belge sistem tarafından otomatik oluşturulmuştur.")
        ]);

        return SimplePdfBuilder.Build(lines);
    }

    private static OrderInvoiceDto ToDto(OrderInvoice entity)
        => new()
        {
            Id = entity.Id,
            InvoiceNumber = entity.InvoiceNumber,
            FileName = entity.FileName,
            TotalAmount = entity.TotalAmount,
            PaidAmount = entity.PaidAmount,
            InvoiceDate = entity.InvoiceDate,
            CreatedAt = entity.CreatedAt
        };

    private readonly record struct PdfTextLine(int FontSize, int X, int Y, string Text);

    private static class SimplePdfBuilder
    {
        public static byte[] Build(IReadOnlyList<PdfTextLine> lines)
        {
            var contentBuilder = new StringBuilder();
            foreach (var line in lines)
            {
                contentBuilder.AppendLine($"BT /F1 {line.FontSize} Tf {line.X} {line.Y} Td ({Escape(Normalize(line.Text))}) Tj ET");
            }

            var content = contentBuilder.ToString();
            var objects = new List<string>
            {
                "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj",
                "2 0 obj << /Type /Pages /Count 1 /Kids [3 0 R] >> endobj",
                "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj",
                "4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj",
                $"5 0 obj << /Length {Encoding.ASCII.GetByteCount(content)} >> stream\n{content}endstream\nendobj"
            };

            var builder = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int> { 0 };
            foreach (var obj in objects)
            {
                offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
                builder.Append(obj).Append('\n');
            }

            var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
            builder.Append("xref\n0 6\n");
            builder.Append("0000000000 65535 f \n");
            for (var i = 1; i <= objects.Count; i++)
            {
                builder.Append(offsets[i].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            }

            builder.Append("trailer << /Size 6 /Root 1 0 R >>\n");
            builder.Append("startxref\n").Append(xrefOffset.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF");
            return Encoding.ASCII.GetBytes(builder.ToString());
        }

        private static string Escape(string text)
            => text.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);

        private static string Normalize(string text)
            => text
                .Replace("İ", "I", StringComparison.Ordinal)
                .Replace("I", "I", StringComparison.Ordinal)
                .Replace("ı", "i", StringComparison.Ordinal)
                .Replace("Ş", "S", StringComparison.Ordinal)
                .Replace("ş", "s", StringComparison.Ordinal)
                .Replace("Ğ", "G", StringComparison.Ordinal)
                .Replace("ğ", "g", StringComparison.Ordinal)
                .Replace("Ü", "U", StringComparison.Ordinal)
                .Replace("ü", "u", StringComparison.Ordinal)
                .Replace("Ö", "O", StringComparison.Ordinal)
                .Replace("ö", "o", StringComparison.Ordinal)
                .Replace("Ç", "C", StringComparison.Ordinal)
                .Replace("ç", "c", StringComparison.Ordinal);
    }
}
