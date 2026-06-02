using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface IAuthService
{
    Task<AuthBootstrapStatusDto> GetBootstrapStatusAsync(CancellationToken cancellationToken = default);
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginCustomerAsync(LoginRequestDto dto, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAdminAsync(LoginRequestDto dto, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginWorkspaceAsync(LoginRequestDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserInfoDto>> GetUsersAsync(CancellationToken cancellationToken = default);
}
