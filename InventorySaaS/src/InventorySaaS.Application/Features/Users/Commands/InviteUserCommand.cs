using System.Security.Cryptography;
using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Users.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Users.Commands;

public record InviteUserCommand(
    string Email,
    string FirstName,
    string LastName,
    List<string> Roles) : IRequest<Result<UserDto>>;

public class InviteUserCommandHandler : IRequestHandler<InviteUserCommand, Result<UserDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;

    public InviteUserCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IEmailService emailService,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<UserDto>> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();

        var emailExists = await _context.Users
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (emailExists)
            return Result<UserDto>.Failure("A user with this email already exists.");

        // Generate a temporary password
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

        // Send invitation email
        var placeholders = new Dictionary<string, string>
        {
            { "FirstName", request.FirstName },
            { "Email", request.Email },
            { "TempPassword", tempPassword }
        };

        await _emailService.SendTemplateAsync(
            request.Email,
            "UserInvitation",
            placeholders,
            cancellationToken);

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
