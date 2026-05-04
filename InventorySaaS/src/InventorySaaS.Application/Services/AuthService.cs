using System.Security.Cryptography;
using System.Text.RegularExpressions;
using InventorySaaS.Application.Features.Auth.DTOs;
using InventorySaaS.Application.Features.Users.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Identity;
using InventorySaaS.Domain.Entities.Tenant;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;

    public AuthService(
        IApplicationDbContext context,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IEmailService emailService)
    {
        _context = context;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterTenantRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.AdminEmail.ToUpperInvariant();

        var emailExists = await _context.Users
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (emailExists)
            throw new ConflictException("An account with this email already exists.");

        var freePlan = await _context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.PlanType == SubscriptionPlanType.Free, cancellationToken)
            ?? throw new BadRequestException("No Free subscription plan found. Please contact support.");

        var slug = GenerateSlug(request.CompanyName);
        var tenant = new TenantInfo
        {
            Name = request.CompanyName,
            Slug = slug,
            Status = TenantStatus.Active,
            SubscriptionPlanId = freePlan.Id,
            ContactEmail = request.AdminEmail
        };

        _context.Tenants.Add(tenant);

        var tenantAdminRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.NormalizedName == AppRoles.TenantAdmin.ToUpperInvariant(), cancellationToken)
            ?? throw new BadRequestException("TenantAdmin role not found. Please contact support.");

        var passwordHash = _passwordHasher.Hash(request.AdminPassword);

        var user = new ApplicationUser
        {
            TenantId = tenant.Id,
            Email = request.AdminEmail,
            NormalizedEmail = normalizedEmail,
            FirstName = request.AdminFirstName,
            LastName = request.AdminLastName,
            PasswordHash = passwordHash,
            PhoneNumber = request.Phone,
            IsActive = true,
            EmailConfirmed = true
        };

        _context.Users.Add(user);

        var userRole = new UserRole { UserId = user.Id, RoleId = tenantAdminRole.Id };
        _context.UserRoles.Add(userRole);

        await _context.SaveChangesAsync(cancellationToken);

        var roles = new List<string> { AppRoles.TenantAdmin };
        var (accessToken, refreshToken) = await _tokenService.GenerateTokensAsync(user, roles);

        var userDto = new UserDto(
            user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber,
            user.IsActive, roles, user.CreatedAt);

        return new AuthResponse(accessToken, refreshToken, DateTime.UtcNow.AddHours(1), userDto);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();

        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Your account has been deactivated.");

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

        var (accessToken, refreshToken) = await _tokenService.GenerateTokensAsync(user, roles);

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(
            user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber,
            user.IsActive, roles, user.CreatedAt);

        return new AuthResponse(accessToken, refreshToken, DateTime.UtcNow.AddHours(1), userDto);
    }

    public async Task<AuthResponse> RefreshTokenAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var result = await _tokenService.RefreshTokenAsync(refreshToken, ipAddress)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        var (accessToken, newRefreshToken) = result;

        var tokenEntity = await _context.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, cancellationToken)
            ?? throw new UnauthorizedAccessException("User not found.");

        var user = tokenEntity.User;
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

        var userDto = new UserDto(
            user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber,
            user.IsActive, roles, user.CreatedAt);

        return new AuthResponse(accessToken, newRefreshToken, DateTime.UtcNow.AddHours(1), userDto);
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.ToUpperInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        // Always silently return if user not found (anti-enumeration)
        if (user is null) return;

        var resetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        user.PasswordResetToken = resetToken;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(2);

        await _context.SaveChangesAsync(cancellationToken);

        var placeholders = new Dictionary<string, string>
        {
            { "FirstName", user.FirstName ?? "User" },
            { "ResetToken", resetToken },
            { "Email", user.Email }
        };

        await _emailService.SendTemplateAsync(user.Email, "PasswordReset", placeholders, cancellationToken);
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken)
            ?? throw new BadRequestException("Invalid request.");

        if (user.PasswordResetToken != request.Token)
            throw new BadRequestException("Invalid or expired reset token.");

        if (user.PasswordResetTokenExpiry is null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
            throw new BadRequestException("Reset token has expired. Please request a new one.");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeTokenAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await _tokenService.RevokeRefreshTokenAsync(refreshToken, ipAddress);
    }

    private static string GenerateSlug(string companyName)
    {
        var slug = companyName.ToLowerInvariant().Trim();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');
        slug += $"-{Guid.NewGuid().ToString()[..6]}";
        return slug;
    }
}
