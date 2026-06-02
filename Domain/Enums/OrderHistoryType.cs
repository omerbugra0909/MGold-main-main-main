namespace MGold.Domain.Enums;

public enum OrderHistoryType
{
    OrderCreated = 1,
    StatusChanged = 2,
    PaymentRecorded = 3,
    PaymentStatusChanged = 4,
    InvoiceGenerated = 5,
    EmailSent = 6,
    SmsSent = 7,
    AdminAction = 8,
    NotificationSent = 9
}
