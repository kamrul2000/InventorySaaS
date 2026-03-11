using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Users.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Users.Commands;

public record CreateUserCommand(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    string? Phone,
    List<string> Roles) : IRequest<Result<UserDto>>;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<UserDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasher _passwordHasher;

    public CreateUserCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService, IPasswordHasher passwordHasher)
    {
        _context = context;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<UserDto>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();

        var emailExists = await _context.Users
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (emailExists)
            return Result<UserDto>.Failure("A user with this email already exists.");

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

        // Assign roles
        var roleNames = request.Roles.Select(r => r.ToUpperInvariant()).ToList();
        var roles = await _context.Roles
            .Where(r => roleNames.Contains(r.NormalizedName))
            .ToListAsync(cancellationToken);

        foreach (var role in roles)
        {
            _context.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.IsActive,
            roles.Select(r => r.Name).ToList(),
            user.CreatedAt);

        return Result<UserDto>.Success(userDto);
    }

}
