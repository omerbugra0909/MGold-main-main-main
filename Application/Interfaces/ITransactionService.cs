using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface ITransactionService
{
    Task<IReadOnlyList<TransactionDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TransactionDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TransactionDto> CreateAsync(CreateTransactionDto dto, CancellationToken cancellationToken = default);
    Task<TransactionDto> UpdateAsync(int id, UpdateTransactionDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
