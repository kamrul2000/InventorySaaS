using InventorySaaS.Application.Features.Auth.DTOs;

namespace InventorySaaS.Application.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterTenantRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);
    Task ForgotPasswordAsync(string email, CancellationToken cancellationToken);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
    Task RevokeTokenAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);
}
