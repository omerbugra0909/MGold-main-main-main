using System.Data;
using Microsoft.EntityFrameworkCore;
using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Application.Mappings;
using MGold.Domain.Constants;
using MGold.Domain.Entities;
using MGold.Domain.Enums;
using MGold.Infrastructure.Data;

namespace MGold.Application.Services;

public class OrderService(
    AppDbContext context,
    IAccessControlService accessControlService,
    INotificationService notificationService,
    IEmailService emailService,
    ISmsService smsService,
    ICurrentUserService currentUserService,
    IOrderHistoryService orderHistoryService,
    IInvoiceService invoiceService,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<IReadOnlyList<OrderDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanReadOperationalData();

        var items = await BuildOrderQuery()
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return items.Select(x => x.ToDto()).ToList();
    }

    public async Task<OrderDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanReadOperationalData();

        var order = await BuildOrderQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Order with id {id} was not found.");

        var dto = order.ToDto();
        dto.Notifications = await context.Notifications
            .AsNoTracking()
            .Where(x => x.RelatedEntityName == nameof(Order) && x.RelatedEntityId == id.ToString())
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new RelatedNotificationDto
            {
                Id = x.Id,
                Title = x.Title,
                Message = x.Message,
                Type = x.Type,
                IsCritical = x.IsCritical,
                IsRead = x.IsRead,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return dto;
    }

    public async Task<OrderDto> CreateAsync(CreateOrderDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanWriteOperationalData();
        ValidateCreateOrder(dto);

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var customer = await context.Customers.FirstOrDefaultAsync(x => x.Id == dto.CustomerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer with id {dto.CustomerId} was not found.");
        accessControlService.EnsureSameCompany(customer.CompanyId);

        var productIds = dto.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await context.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var order = new Order
        {
            CustomerId = customer.Id,
            OrderNumber = CreateOrderNumber(),
            Status = OrderStatus.Preparing,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            DueDate = dto.DueDate,
            PreferredPaymentMethod = dto.PreferredPaymentMethod,
            PaymentStatus = PaymentStatus.Pending,
            CompanyId = customer.CompanyId ?? currentUserService.CompanyId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var item in dto.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                throw new KeyNotFoundException($"Product with id {item.ProductId} was not found.");
            }
            accessControlService.EnsureSameCompany(product.CompanyId);

            if (item.Quantity > product.StockQuantity)
            {
                throw new BusinessRuleException($"{product.Name} icin yeterli stok yok.");
            }

            var unitPrice = item.UnitPriceOverride.GetValueOrDefault(product.SalePrice);
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                TotalPrice = unitPrice * item.Quantity,
                Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim()
            });

            product.StockQuantity -= item.Quantity;
        }

        order.TotalAmount = order.Items.Sum(x => x.TotalPrice);

        await context.Orders.AddAsync(order, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await orderHistoryService.RecordAsync(
            order.Id,
            OrderHistoryType.OrderCreated,
            "Siparis olusturuldu",
            $"{order.OrderNumber} numarali siparis {customer.Name} icin olusturuldu.",
            cancellationToken: cancellationToken);

        await CreateOrderNotificationAsync(
            order,
            "Yeni siparis olusturuldu",
            $"{order.OrderNumber} numarali siparis {customer.Name} icin olusturuldu.",
            NotificationType.NewOrder,
            cancellationToken);

        await notificationService.GenerateLowStockNotificationsAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            try
            {
                await emailService.SendAsync(new SendEmailRequestDto
                {
                    To = customer.Email,
                    Subject = $"Siparisiniz alindi: {order.OrderNumber}",
                    HtmlBody = $"<p>Merhaba {System.Net.WebUtility.HtmlEncode(customer.Name)},</p><p>{order.OrderNumber} numarali siparisiniz olusturuldu.</p><p>Toplam tutar: <strong>{order.TotalAmount:N2}</strong></p>"
                }, cancellationToken);

                await orderHistoryService.RecordAsync(
                    order.Id,
                    OrderHistoryType.EmailSent,
                    "Siparis e-postasi gonderildi",
                    $"{customer.Email} adresine siparis olusturma bilgilendirmesi gonderildi.",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Order confirmation email could not be sent for order {OrderId}.", order.Id);
            }
        }

        try
        {
            await smsService.SendAsync(new SendSmsRequestDto
            {
                ToPhone = customer.Phone,
                Message = $"{order.OrderNumber} numarali siparisiniz alindi. Tutar: {order.TotalAmount:N2} TL"
            }, cancellationToken);

            await orderHistoryService.RecordAsync(
                order.Id,
                OrderHistoryType.SmsSent,
                "Siparis SMS'i gonderildi",
                $"{customer.Phone} numarasina siparis bilgilendirme SMS'i gonderildi.",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Order confirmation SMS could not be sent for order {OrderId}.", order.Id);
        }
        return await GetByIdAsync(order.Id, cancellationToken);
    }

    public async Task<OrderDto> UpdateStatusAsync(int id, UpdateOrderStatusDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureManagerOrSystemAdmin();
        EnsureDefinedEnum(dto.Status, "Order status");

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var order = await BuildOrderQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Order with id {id} was not found.");

        if (!OrderPolicy.IsAllowedStatusTransition(order.Status, dto.Status))
        {
            throw new BusinessRuleException($"{order.Status} durumundan {dto.Status} durumuna gecilemez.");
        }

        if (dto.Status == OrderStatus.Completed && order.PaymentStatus != PaymentStatus.Paid)
        {
            throw new BusinessRuleException("Siparis tamamlanmadan once odeme tam olarak tahsil edilmelidir.");
        }

        var previousStatus = order.Status;
        if (dto.Status == OrderStatus.Cancelled && order.Status != OrderStatus.Cancelled)
        {
            foreach (var item in order.Items)
            {
                if (item.Product is not null)
                {
                    item.Product.StockQuantity += item.Quantity;
                }
            }
        }

        order.Status = dto.Status;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            order.Notes = dto.Notes.Trim();
        }

        order.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        await orderHistoryService.RecordAsync(
            order.Id,
            OrderHistoryType.StatusChanged,
            "Siparis durumu guncellendi",
            $"{previousStatus} durumundan {order.Status} durumuna gecildi.",
            cancellationToken: cancellationToken);

        await CreateOrderNotificationAsync(
            order,
            "Siparis durumu guncellendi",
            $"{order.OrderNumber} numarali siparis durumu {order.Status} oldu.",
            dto.Status == OrderStatus.Cancelled ? NotificationType.Warning : NotificationType.Info,
            cancellationToken,
            dto.Status == OrderStatus.Cancelled);

        if (order.Status == OrderStatus.Completed && order.Invoices.Count == 0)
        {
            await invoiceService.GenerateForOrderAsync(order.Id, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(order.Customer.Email))
        {
            try
            {
                await emailService.SendAsync(new SendEmailRequestDto
                {
                    To = order.Customer.Email,
                    Subject = $"Siparis durumu guncellendi: {order.OrderNumber}",
                    HtmlBody = $"<p>Merhaba {System.Net.WebUtility.HtmlEncode(order.Customer.Name)},</p><p>{order.OrderNumber} numarali siparisinizin yeni durumu: <strong>{order.Status}</strong></p>"
                }, cancellationToken);

                await orderHistoryService.RecordAsync(
                    order.Id,
                    OrderHistoryType.EmailSent,
                    "Durum e-postasi gonderildi",
                    $"{order.Customer.Email} adresine yeni siparis durumu bilgisi gonderildi.",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Order status email could not be sent for order {OrderId}.", order.Id);
            }
        }

        try
        {
            await smsService.SendAsync(new SendSmsRequestDto
            {
                ToPhone = order.Customer.Phone,
                Message = $"{order.OrderNumber} siparis durumunuz: {order.Status}"
            }, cancellationToken);

            await orderHistoryService.RecordAsync(
                order.Id,
                OrderHistoryType.SmsSent,
                "Durum SMS'i gonderildi",
                $"{order.Customer.Phone} numarasina yeni siparis durumu bilgisi gonderildi.",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Order status SMS could not be sent for order {OrderId}.", order.Id);
        }

        return await GetByIdAsync(order.Id, cancellationToken);
    }

    public async Task<OrderDto> AddPaymentAsync(int id, CreateOrderPaymentDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureManagerOrSystemAdmin();
        OrderPolicy.ValidatePayment(dto);

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var order = await BuildOrderQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Order with id {id} was not found.");

        if (order.Status == OrderStatus.Cancelled)
        {
            throw new BusinessRuleException("Cancelled orders cannot receive payments.");
        }

        if (!string.IsNullOrWhiteSpace(dto.IdempotencyKey)
            && order.Payments.Any(x => x.IdempotencyKey == dto.IdempotencyKey.Trim()))
        {
            throw new BusinessRuleException("Bu odeme istegi daha once islenmis.");
        }

        if (!string.IsNullOrWhiteSpace(dto.ProviderTransactionId)
            && order.Payments.Any(x =>
                string.Equals(x.ProviderKey, OrderPolicy.NormalizeProviderKey(dto.ProviderKey), StringComparison.OrdinalIgnoreCase)
                && x.ProviderTransactionId == dto.ProviderTransactionId.Trim()))
        {
            throw new BusinessRuleException("Bu saglayici islem referansi daha once kaydedilmis.");
        }

        OrderPolicy.ValidateRefundParent(dto, order);

        var currentPaidAmount = OrderPolicy.CalculatePaidAmount(order.Payments);
        if (dto.Status is PaymentStatus.Paid or PaymentStatus.PartiallyPaid)
        {
            if (currentPaidAmount + dto.Amount > order.TotalAmount)
            {
                throw new BusinessRuleException("Odeme tutari siparis toplam tutarini asamaz.");
            }
        }
        else if (dto.Status is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded or PaymentStatus.RefundPending && dto.Amount > currentPaidAmount)
        {
            throw new BusinessRuleException("Iade tutari odenmis tutari asamaz.");
        }

        var payment = new OrderPayment
        {
            OrderId = order.Id,
            Method = dto.Method,
            Status = dto.Status,
            Amount = dto.Amount,
            ReferenceNumber = string.IsNullOrWhiteSpace(dto.ReferenceNumber) ? null : dto.ReferenceNumber.Trim(),
            ProviderKey = OrderPolicy.NormalizeProviderKey(dto.ProviderKey),
            ProviderTransactionId = string.IsNullOrWhiteSpace(dto.ProviderTransactionId) ? null : dto.ProviderTransactionId.Trim(),
            IdempotencyKey = string.IsNullOrWhiteSpace(dto.IdempotencyKey) ? null : dto.IdempotencyKey.Trim(),
            InstallmentCount = dto.InstallmentCount,
            RequiresThreeDSecure = dto.RequiresThreeDSecure,
            ThreeDSecureStatus = string.IsNullOrWhiteSpace(dto.ThreeDSecureStatus) ? null : dto.ThreeDSecureStatus.Trim(),
            ParentPaymentId = dto.ParentPaymentId,
            IsRefund = dto.IsRefund || dto.Status is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded or PaymentStatus.RefundPending,
            IsPartialRefund = dto.Status == PaymentStatus.PartiallyRefunded,
            FailureCode = string.IsNullOrWhiteSpace(dto.FailureCode) ? null : dto.FailureCode.Trim(),
            FailureMessage = string.IsNullOrWhiteSpace(dto.FailureMessage) ? null : dto.FailureMessage.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            PaidAt = dto.PaidAt ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedByUsername = currentUserService.Username
        };

        var paymentsForStatus = order.Payments.Append(payment).ToList();
        order.PreferredPaymentMethod = dto.Method;
        order.PaidAmount = OrderPolicy.CalculatePaidAmount(paymentsForStatus);
        order.PaymentStatus = OrderPolicy.ResolvePaymentStatus(order.TotalAmount, order.PaidAmount, paymentsForStatus);
        order.UpdatedAt = DateTime.UtcNow;

        await context.OrderPayments.AddAsync(payment, cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintFailure(ex))
        {
            throw new BusinessRuleException("Bu odeme referansi veya idempotency anahtari daha once islenmis.");
        }

        await orderHistoryService.RecordAsync(
            order.Id,
            OrderHistoryType.PaymentRecorded,
            "Odeme kaydi eklendi",
            $"{dto.Amount:N2} TL tutarinda {dto.Method} odemesi {dto.Status} durumuyla kaydedildi.",
            nameof(OrderPayment),
            payment.Id.ToString(),
            cancellationToken: cancellationToken);

        await CreateOrderNotificationAsync(
            order,
            "Odeme kaydi eklendi",
            $"{order.OrderNumber} icin {dto.Amount:N2} TL tutarinda {dto.Method} odeme kaydi olusturuldu.",
            NotificationType.Info,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await GetByIdAsync(order.Id, cancellationToken);
    }

    private IQueryable<Order> BuildOrderQuery()
    {
        IQueryable<Order> query = context.Orders
            .Include(x => x.Customer)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .Include(x => x.Payments)
            .Include(x => x.Invoices)
            .Include(x => x.HistoryEntries);

        if (!currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            query = query.Where(x => x.CompanyId == currentUserService.CompanyId);
        }

        return query;
    }

    private static string CreateOrderNumber()
        => $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32].ToUpperInvariant();

    private static void ValidateCreateOrder(CreateOrderDto dto)
    {
        if (dto.CustomerId <= 0)
        {
            throw new BusinessRuleException("Customer is required.");
        }

        if (dto.PreferredPaymentMethod.HasValue)
        {
            EnsureDefinedEnum(dto.PreferredPaymentMethod.Value, "Preferred payment method");
        }

        if (dto.Items is null || dto.Items.Count == 0)
        {
            throw new BusinessRuleException("Order must contain at least one product.");
        }

        foreach (var item in dto.Items)
        {
            if (item.ProductId <= 0)
            {
                throw new BusinessRuleException("Order item product is required.");
            }

            if (item.Quantity <= 0)
            {
                throw new BusinessRuleException("Order item quantity must be greater than zero.");
            }

            if (item.UnitPriceOverride is < 0)
            {
                throw new BusinessRuleException("Order item unit price cannot be negative.");
            }
        }
    }

    private static void ValidatePayment(CreateOrderPaymentDto dto)
    {
        EnsureDefinedEnum(dto.Method, "Payment method");
        EnsureDefinedEnum(dto.Status, "Payment status");

        if (dto.Amount <= 0)
        {
            throw new BusinessRuleException("Payment amount must be greater than zero.");
        }

        if (dto.InstallmentCount < 1 || dto.InstallmentCount > 36)
        {
            throw new BusinessRuleException("Taksit sayisi 1 ile 36 arasinda olmalidir.");
        }

        if (dto.Method is PaymentMethod.Card or PaymentMethod.CreditCard or PaymentMethod.DebitCard
            && dto.Status is PaymentStatus.Paid or PaymentStatus.PartiallyPaid
            && string.IsNullOrWhiteSpace(dto.ReferenceNumber))
        {
            throw new BusinessRuleException("Kart odemelerinde provizyon veya islem referansi girilmelidir.");
        }

        if (dto.Method == PaymentMethod.DebitCard && dto.InstallmentCount > 1)
        {
            throw new BusinessRuleException("Banka karti odemelerinde taksit kullanilamaz.");
        }

        if (dto.Status == PaymentStatus.Failed
            && string.IsNullOrWhiteSpace(dto.FailureCode)
            && string.IsNullOrWhiteSpace(dto.FailureMessage))
        {
            throw new BusinessRuleException("Basarisiz odeme kayitlarinda hata kodu veya mesaji bulunmalidir.");
        }

        var providerKey = NormalizeProviderKey(dto.ProviderKey);
        var isProviderPayment = providerKey != ManualPaymentProvider.Key;
        if (isProviderPayment && string.IsNullOrWhiteSpace(dto.IdempotencyKey))
        {
            throw new BusinessRuleException("Saglayici odemelerinde idempotency anahtari zorunludur.");
        }

        if (isProviderPayment
            && dto.Status is PaymentStatus.Paid or PaymentStatus.PartiallyPaid or PaymentStatus.Authorized
            && string.IsNullOrWhiteSpace(dto.ProviderTransactionId))
        {
            throw new BusinessRuleException("Saglayici odemelerinde provider transaction id zorunludur.");
        }

        if (dto.IsRefund && !dto.ParentPaymentId.HasValue)
        {
            throw new BusinessRuleException("Iade kayitlarinda ana odeme secilmelidir.");
        }
    }

    private static void ValidateRefundParent(CreateOrderPaymentDto dto, Order order)
    {
        var isRefund = dto.IsRefund || dto.Status is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded or PaymentStatus.RefundPending;
        if (!isRefund)
        {
            return;
        }

        if (!dto.ParentPaymentId.HasValue)
        {
            throw new BusinessRuleException("Iade kayitlarinda ana odeme secilmelidir.");
        }

        var parent = order.Payments.FirstOrDefault(x => x.Id == dto.ParentPaymentId.Value);
        if (parent is null)
        {
            throw new BusinessRuleException("Iade edilecek ana odeme bu siparise ait degil.");
        }

        if (parent.Status is not (PaymentStatus.Paid or PaymentStatus.PartiallyPaid))
        {
            throw new BusinessRuleException("Yalnizca tahsil edilmis odemeler iade edilebilir.");
        }

        var alreadyRefunded = order.Payments
            .Where(x => x.ParentPaymentId == parent.Id && x.Status is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded)
            .Sum(x => x.Amount);
        if (alreadyRefunded + dto.Amount > parent.Amount)
        {
            throw new BusinessRuleException("Iade tutari ana odemenin kalan tutarini asamaz.");
        }
    }

    private static void EnsureDefinedEnum<TEnum>(TEnum value, string fieldName) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new BusinessRuleException($"{fieldName} is invalid.");
        }
    }

    private async Task CreateOrderNotificationAsync(
        Order order,
        string title,
        string message,
        NotificationType type,
        CancellationToken cancellationToken,
        bool isCritical = false)
    {
        var notification = await notificationService.CreateAsync(new CreateNotificationDto
        {
            Title = title,
            Message = message,
            Type = type,
            TargetRole = RoleConstants.ManagerOnly,
            RelatedEntityName = nameof(Order),
            RelatedEntityId = order.Id.ToString(),
            IsCritical = isCritical
        }, cancellationToken);

        await orderHistoryService.RecordAsync(
            order.Id,
            OrderHistoryType.NotificationSent,
            "Sistem bildirimi olusturuldu",
            $"{notification.Title} bildirimi role dayali akisa eklendi.",
            nameof(Domain.Entities.Notification),
            notification.Id.ToString(),
            cancellationToken: cancellationToken);
    }

    private static decimal CalculatePaidAmount(IEnumerable<OrderPayment> payments)
    {
        var paid = payments
            .Where(x => x.Status is PaymentStatus.Paid or PaymentStatus.PartiallyPaid)
            .Sum(x => x.Amount);
        var refunded = payments
            .Where(x => x.Status is PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded)
            .Sum(x => x.Amount);
        return Math.Max(paid - refunded, 0m);
    }

    private static PaymentStatus ResolvePaymentStatus(decimal totalAmount, decimal paidAmount, IEnumerable<OrderPayment> payments)
    {
        if (paidAmount >= totalAmount && totalAmount > 0)
        {
            return PaymentStatus.Paid;
        }

        if (paidAmount > 0)
        {
            return PaymentStatus.PartiallyPaid;
        }

        if (payments.Any(x => x.Status == PaymentStatus.RefundPending))
        {
            return PaymentStatus.RefundPending;
        }

        if (payments.Any(x => x.Status == PaymentStatus.PartiallyRefunded))
        {
            return PaymentStatus.PartiallyRefunded;
        }

        if (payments.Any(x => x.Status == PaymentStatus.Refunded))
        {
            return PaymentStatus.Refunded;
        }

        if (payments.Any(x => x.Status == PaymentStatus.Failed))
        {
            return PaymentStatus.Failed;
        }

        if (payments.Any() && payments.All(x => x.Status == PaymentStatus.Cancelled))
        {
            return PaymentStatus.Cancelled;
        }

        return PaymentStatus.Pending;
    }

    private static string NormalizeProviderKey(string? providerKey)
        => string.IsNullOrWhiteSpace(providerKey) ? ManualPaymentProvider.Key : providerKey.Trim().ToLowerInvariant();

    private static bool IsAllowedStatusTransition(OrderStatus current, OrderStatus next)
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

    private static bool IsUniqueConstraintFailure(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("IX_OrderPayments", StringComparison.OrdinalIgnoreCase);
    }
}
