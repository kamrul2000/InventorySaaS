using InventorySaaS.Domain.Entities.Identity;

namespace InventorySaaS.Application.Interfaces;

public interface ITokenService
{
    Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(ApplicationUser user, IList<string> roles);
    Task<(string AccessToken, string RefreshToken)?> RefreshTokenAsync(string refreshToken, string? ipAddress);
    Task RevokeRefreshTokenAsync(string refreshToken, string? ipAddress);

    /// <summary>
    /// How long an access token minted by GenerateTokensAsync/RefreshTokenAsync stays valid for -
    /// the single source of truth for JwtSettings:ExpiryMinutes, so callers reporting an
    /// AuthResponse.ExpiresAt don't hardcode a second, independently-maintained value (AUTH-08).
    /// </summary>
    TimeSpan GetAccessTokenLifetime();
}
