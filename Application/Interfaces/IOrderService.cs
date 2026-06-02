using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface IOrderService
{
    Task<IReadOnlyList<OrderDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<OrderDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<OrderDto> CreateAsync(CreateOrderDto dto, CancellationToken cancellationToken = default);
    Task<OrderDto> UpdateStatusAsync(int id, UpdateOrderStatusDto dto, CancellationToken cancellationToken = default);
    Task<OrderDto> AddPaymentAsync(int id, CreateOrderPaymentDto dto, CancellationToken cancellationToken = default);
}
