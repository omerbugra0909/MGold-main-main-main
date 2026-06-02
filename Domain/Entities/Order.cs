using System.ComponentModel.DataAnnotations;
using MGold.Domain.Enums;

namespace MGold.Domain.Entities;

public class Order
{
    public int Id { get; set; }

    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    [Required]
    [MaxLength(40)]
    public string OrderNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public OrderStatus Status { get; set; } = OrderStatus.Preparing;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public PaymentMethod? PreferredPaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }

    public int? AssignedEmployeeUserId { get; set; }
    public AppUser? AssignedEmployeeUser { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderPayment> Payments { get; set; } = new List<OrderPayment>();
    public ICollection<OrderInvoice> Invoices { get; set; } = new List<OrderInvoice>();
    public ICollection<OrderHistoryEntry> HistoryEntries { get; set; } = new List<OrderHistoryEntry>();
}
