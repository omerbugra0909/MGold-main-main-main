using System.ComponentModel.DataAnnotations;

namespace MGold.Domain.Entities;

public class OrderInvoice
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    [Required]
    [MaxLength(40)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(260)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    [MaxLength(160)]
    public string FileName { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
