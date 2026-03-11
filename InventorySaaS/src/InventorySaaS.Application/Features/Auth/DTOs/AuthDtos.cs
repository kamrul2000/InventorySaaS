using InventorySaaS.Application.Features.Users.DTOs;

namespace InventorySaaS.Application.Features.Auth.DTOs;

public record LoginRequest(string Email, string Password);

public record RegisterTenantRequest(
    string CompanyName,
    string AdminEmail,
    string AdminPassword,
    string AdminFirstName,
    string AdminLastName,
    string? Phone);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User);

public record RefreshTokenRequest(string RefreshToken);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);
