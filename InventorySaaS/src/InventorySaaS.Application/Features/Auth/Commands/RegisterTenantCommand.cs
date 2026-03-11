using System.Text.RegularExpressions;
using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Auth.DTOs;
using InventorySaaS.Application.Features.Users.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Entities.Identity;
using InventorySaaS.Domain.Entities.Tenant;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Auth.Commands;

public record RegisterTenantCommand(
    string CompanyName,
    string AdminEmail,
    string AdminPassword,
    string AdminFirstName,
    string AdminLastName,
    string? Phone) : IRequest<Result<AuthResponse>>;

public class RegisterTenantCommandHandler : IRequestHandler<RegisterTenantCommand, Result<AuthResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterTenantCommandHandler(IApplicationDbContext context, ITokenService tokenService, IPasswordHasher passwordHasher)
    {
        _context = context;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.AdminEmail.ToUpperInvariant();

        var emailExists = await _context.Users
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (emailExists)
            return Result<AuthResponse>.Failure("An account with this email already exists.");

        // Find the Free subscription plan
        var freePlan = await _context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.PlanType == SubscriptionPlanType.Free, cancellationToken);

        if (freePlan is null)
            return Result<AuthResponse>.Failure("No Free subscription plan found. Please contact support.");

        // Create tenant
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

        // Find TenantAdmin role
        var tenantAdminRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.NormalizedName == AppRoles.TenantAdmin.ToUpperInvariant(), cancellationToken);

        if (tenantAdminRole is null)
            return Result<AuthResponse>.Failure("TenantAdmin role not found. Please contact support.");

        // Create admin user
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

        // Assign TenantAdmin role
        var userRole = new UserRole
        {
            UserId = user.Id,
            RoleId = tenantAdminRole.Id
        };

        _context.UserRoles.Add(userRole);

        await _context.SaveChangesAsync(cancellationToken);

        // Generate tokens
        var roles = new List<string> { AppRoles.TenantAdmin };
        var (accessToken, refreshToken) = await _tokenService.GenerateTokensAsync(user, roles);

        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.IsActive,
            roles,
            user.CreatedAt);

        var response = new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddHours(1),
            userDto);

        return Result<AuthResponse>.Success(response);
    }

    private static string GenerateSlug(string companyName)
    {
        var slug = companyName.ToLowerInvariant().Trim();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');

        // Append random suffix to ensure uniqueness
        slug += $"-{Guid.NewGuid().ToString()[..6]}";
        return slug;
    }

}
