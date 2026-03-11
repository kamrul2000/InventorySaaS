using InventorySaaS.Domain.Entities.Identity;

namespace InventorySaaS.Application.Interfaces;

public interface ITokenService
{
    Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(ApplicationUser user, IList<string> roles);
    Task<(string AccessToken, string RefreshToken)?> RefreshTokenAsync(string refreshToken, string? ipAddress);
    Task RevokeRefreshTokenAsync(string refreshToken, string? ipAddress);
}
