using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Users.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Entities.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Users.Commands;

public record UpdateUserCommand(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? Phone,
    bool? IsActive,
    List<string>? Roles) : IRequest<Result<UserDto>>;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result<UserDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateUserCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<UserDto>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null)
            return Result<UserDto>.Failure("User not found.");

        if (request.FirstName is not null)
            user.FirstName = request.FirstName;

        if (request.LastName is not null)
            user.LastName = request.LastName;

        if (request.Phone is not null)
            user.PhoneNumber = request.Phone;

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        // Update roles if provided
        if (request.Roles is not null)
        {
            // Remove existing roles
            var existingUserRoles = await _context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .ToListAsync(cancellationToken);

            foreach (var ur in existingUserRoles)
                _context.UserRoles.Remove(ur);

            // Add new roles
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
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Reload roles
        var currentRoles = await _context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role.Name)
            .ToListAsync(cancellationToken);

        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.IsActive,
            currentRoles,
            user.CreatedAt);

        return Result<UserDto>.Success(userDto);
    }
}
