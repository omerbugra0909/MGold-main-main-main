using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Domain.Entities;
using MGold.Domain.Enums;

namespace MGold.Application.Services;

public static class OrderPolicy
{
    public static void ValidatePayment(CreateOrderPaymentDto dto)
    {
        EnsureDefinedEnum(dto.Method, "Payment method");
        EnsureDefinedEnum(dto.Status, "Payment status");

        if (dto.Amount <= 0)
        {
            throw new BusinessRuleException("Payment amount must be greater than zero.");
        }

        if (dto.InstallmentCount < 1 || dto.InstallmentCount > 36)
        {
            throw new BusinessRuleException("Taksit sayisi 1 ile 36 arasinda olmalıdir.");
        }

        if (dto.Method is PaymentMethod.Card or PaymentMethod.CreditCard or PaymentMethod.DebitCard
            && dto.Status is PaymentStatus.Paid or PaymentStatus.PartiallyPaid
            && string.IsNullOrWhiteSpace(dto.ReferenceNumber))
        {
            throw new BusinessRuleException("Kart ödemelerinde provizyon veya işlem referansi girilmelidir.");
        }

        if (dto.Method == PaymentMethod.DebitCard && dto.InstallmentCount > 1)
        {
            throw new BusinessRuleException("Banka kartı ödemelerinde taksit kullanılamaz.");
        }

        if (dto.Status == PaymentStatus.Failed
            && string.IsNullOrWhiteSpace(dto.FailureCode)
            && string.IsNullOrWhiteSpace(dto.FailureMessage))
        {
            throw new BusinessRuleException("Basarisiz ödeme kayıtlarinda hata kodu veya mesaji bulunmalidir.");
        }

        var providerKey = NormalizeProviderKey(dto.ProviderKey);
        var isProviderPayment = providerKey != ManualPaymentProvider.Key;
        if (isProviderPayment && string.IsNullOrWhiteSpace(dto.IdempotencyKey))
        {
            throw new BusinessRuleException("Saglayici ödemelerinde idempotency anahtari zorunludur.");
        }

        if (isProviderPayment
            && dto.Status is PaymentStatus.Paid or PaymentStatus.PartiallyPaid or PaymentStatus.Authorized
            && string.IsNullOrWhiteSpace(dto.ProviderTransactionId))
        {
            throw new BusinessRuleException("Saglayici ödemelerinde provider transaction id zorunludur.");
        }

        if (dto.IsRefund && !dto.ParentPaymentId.HasValue)
        {
            throw new BusinessRuleException("İade kayıtlarinda ana ödeme secilmelidir.");
        }
    }

    public static void ValidateRefundParent(CreateOrderPaymentDto dto, Order order)
    {
        var isRefund = dto.IsRefund || dto.Status is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded or PaymentStatus.RefundPending;
        if (!isRefund)
        {
            return;
        }

        if (!dto.ParentPaymentId.HasValue)
        {
            throw new BusinessRuleException("İade kayıtlarinda ana ödeme secilmelidir.");
        }

        var parent = order.Payments.FirstOrDefault(x => x.Id == dto.ParentPaymentId.Value);
        if (parent is null)
        {
            throw new BusinessRuleException("İade edilecek ana ödeme bu siparişe ait değil.");
        }

        if (parent.Status is not (PaymentStatus.Paid or PaymentStatus.PartiallyPaid))
        {
            throw new BusinessRuleException("Yalnızca tahsil edilmis ödemeler iade edilebilir.");
        }

        var alreadyRefunded = order.Payments
            .Where(x => x.ParentPaymentId == parent.Id && x.Status is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded)
            .Sum(x => x.Amount);
        if (alreadyRefunded + dto.Amount > parent.Amount)
        {
            throw new BusinessRuleException("İade tutari ana ödemenin kalan tutarini aşamaz.");
        }
    }

    public static decimal CalculatePaidAmount(IEnumerable<OrderPayment> payments)
    {
        var paid = payments
            .Where(x => x.Status is PaymentStatus.Paid or PaymentStatus.PartiallyPaid)
            .Sum(x => x.Amount);
        var refunded = payments
            .Where(x => x.Status is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded)
            .Sum(x => x.Amount);
        return Math.Max(paid - refunded, 0m);
    }

    public static PaymentStatus ResolvePaymentStatus(decimal totalAmount, decimal paidAmount, IEnumerable<OrderPayment> payments)
    {
        var paymentList = payments.ToList();
        if (paidAmount >= totalAmount && totalAmount > 0)
        {
            return PaymentStatus.Paid;
        }

        if (paidAmount > 0)
        {
            return PaymentStatus.PartiallyPaid;
        }

        if (paymentList.Any(x => x.Status == PaymentStatus.RefundPending))
        {
            return PaymentStatus.RefundPending;
        }

        if (paymentList.Any(x => x.Status == PaymentStatus.PartiallyRefunded))
        {
            return PaymentStatus.PartiallyRefunded;
        }

        if (paymentList.Any(x => x.Status == PaymentStatus.Refunded))
        {
            return PaymentStatus.Refunded;
        }

        if (paymentList.Any(x => x.Status == PaymentStatus.Failed))
        {
            return PaymentStatus.Failed;
        }

        if (paymentList.Any() && paymentList.All(x => x.Status == PaymentStatus.Cancelled))
        {
            return PaymentStatus.Cancelled;
        }

        return PaymentStatus.Pending;
    }

    public static bool IsAllowedStatusTransition(OrderStatus current, OrderStatus next)
        => current == next
            || (current, next) switch
            {
                (OrderStatus.Preparing, OrderStatus.PaymentPending) => true,
                (OrderStatus.Preparing, OrderStatus.Ready) => true,
                (OrderStatus.Preparing, OrderStatus.Cancelled) => true,
                (OrderStatus.PaymentPending, OrderStatus.Ready) => true,
                (OrderStatus.PaymentPending, OrderStatus.Cancelled) => true,
                (OrderStatus.Ready, OrderStatus.Completed) => true,
                (OrderStatus.Ready, OrderStatus.Cancelled) => true,
                _ => false
            };

    public static string NormalizeProviderKey(string? providerKey)
        => string.IsNullOrWhiteSpace(providerKey) ? ManualPaymentProvider.Key : providerKey.Trim().ToLowerInvariant();

    private static void EnsureDefinedEnum<TEnum>(TEnum value, string fieldName) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new BusinessRuleException($"{fieldName} is invalid.");
        }
    }
}
