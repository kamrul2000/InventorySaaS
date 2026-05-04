using System.Security.Cryptography;
using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Users.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Identity;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class UserService : IUserService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;

    public UserService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPasswordHasher passwordHasher,
        IEmailService emailService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
    }

    public async Task<PaginatedList<UserDto>> GetAllAsync(
        PaginationParams pagination,
        CancellationToken cancellationToken)
    {
        var query = _context.Users
            .Where(u => !u.IsDeleted)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(u =>
                u.Email.ToLower().Contains(searchTerm) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(searchTerm)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(searchTerm)));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "email" => pagination.SortDescending ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            "name" => pagination.SortDescending ? query.OrderByDescending(u => u.FirstName) : query.OrderBy(u => u.FirstName),
            _ => query.OrderByDescending(u => u.CreatedAt)
        };

        var projected = query.Select(u => new UserDto(
            u.Id, u.Email, u.FirstName, u.LastName, u.PhoneNumber, u.IsActive,
            u.UserRoles.Select(ur => ur.Role.Name).ToList(), u.CreatedAt));

        return await PaginatedList<UserDto>.CreateAsync(
            projected, pagination.PageNumber, pagination.PageSize, cancellationToken);
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Where(u => u.Id == id && !u.IsDeleted)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(ApplicationUser), id);

        return new UserDto(
            user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber, user.IsActive,
            user.UserRoles.Select(ur => ur.Role.Name).ToList(), user.CreatedAt);
    }

    public async Task<UserDto> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();

        var emailExists = await _context.Users
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (emailExists)
            throw new ConflictException("A user with this email already exists.");

        var passwordHash = _passwordHasher.Hash(request.Password);

        var user = new ApplicationUser
        {
            TenantId = _currentUserService.TenantId,
            Email = request.Email,
            NormalizedEmail = normalizedEmail,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PasswordHash = passwordHash,
            PhoneNumber = request.Phone,
            IsActive = true,
            EmailConfirmed = true
        };

        _context.Users.Add(user);

        var roleNames = request.Roles.Select(r => r.ToUpperInvariant()).ToList();
        var roles = await _context.Roles
            .Where(r => roleNames.Contains(r.NormalizedName))
            .ToListAsync(cancellationToken);

        foreach (var role in roles)
        {
            _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new UserDto(
            user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber, user.IsActive,
            roles.Select(r => r.Name).ToList(), user.CreatedAt);
    }

    public async Task<UserDto> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(ApplicationUser), id);

        if (request.FirstName is not null) user.FirstName = request.FirstName;
        if (request.LastName is not null) user.LastName = request.LastName;
        if (request.Phone is not null) user.PhoneNumber = request.Phone;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        if (request.Roles is not null)
        {
            var existingUserRoles = await _context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .ToListAsync(cancellationToken);

            foreach (var ur in existingUserRoles)
                _context.UserRoles.Remove(ur);

            var roleNames = request.Roles.Select(r => r.ToUpperInvariant()).ToList();
            var roles = await _context.Roles
                .Where(r => roleNames.Contains(r.NormalizedName))
                .ToListAsync(cancellationToken);

            foreach (var role in roles)
            {
                _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var currentRoles = await _context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role.Name)
            .ToListAsync(cancellationToken);

        return new UserDto(
            user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber, user.IsActive,
            currentRoles, user.CreatedAt);
    }

    public async Task InviteAsync(
        InviteUserRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();

        var emailExists = await _context.Users
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (emailExists)
            throw new ConflictException("A user with this email already exists.");

        var tempPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));
        var passwordHash = _passwordHasher.Hash(tempPassword);

        var user = new ApplicationUser
        {
            TenantId = _currentUserService.TenantId,
            Email = request.Email,
            NormalizedEmail = normalizedEmail,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PasswordHash = passwordHash,
            IsActive = true,
            EmailConfirmed = false
        };

        _context.Users.Add(user);

        var roleNames = request.Roles.Select(r => r.ToUpperInvariant()).ToList();
        var roles = await _context.Roles
            .Where(r => roleNames.Contains(r.NormalizedName))
            .ToListAsync(cancellationToken);

        foreach (var role in roles)
        {
            _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }

        await _context.SaveChangesAsync(cancellationToken);

        var placeholders = new Dictionary<string, string>
        {
            { "FirstName", request.FirstName },
            { "Email", request.Email },
            { "TempPassword", tempPassword }
        };

        await _emailService.SendTemplateAsync(request.Email, "UserInvitation", placeholders, cancellationToken);
    }
}
