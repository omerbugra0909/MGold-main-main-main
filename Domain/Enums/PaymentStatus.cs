namespace MGold.Domain.Enums;

public enum PaymentStatus
{
    Pending = 1,
    Paid = 2,
    PartiallyPaid = 3,
    Cancelled = 4,
    Refunded = 5,
    Failed = 6,
    Authorized = 7,
    RefundPending = 8,
    PartiallyRefunded = 9
}
