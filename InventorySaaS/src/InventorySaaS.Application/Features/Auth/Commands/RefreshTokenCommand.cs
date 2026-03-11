using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Auth.DTOs;
using InventorySaaS.Application.Features.Users.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken, string? IpAddress = null) : IRequest<Result<AuthResponse>>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly ITokenService _tokenService;
    private readonly IApplicationDbContext _context;

    public RefreshTokenCommandHandler(ITokenService tokenService, IApplicationDbContext context)
    {
        _tokenService = tokenService;
        _context = context;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var result = await _tokenService.RefreshTokenAsync(request.RefreshToken, request.IpAddress);

        if (result is null)
            return Result<AuthResponse>.Failure("Invalid or expired refresh token.");

        var (accessToken, newRefreshToken) = result.Value;

        // Find the user associated with the original refresh token to build the response
        var tokenEntity = await _context.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

        if (tokenEntity?.User is null)
            return Result<AuthResponse>.Failure("User not found.");

        var user = tokenEntity.User;
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

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
            newRefreshToken,
            DateTime.UtcNow.AddHours(1),
            userDto);

        return Result<AuthResponse>.Success(response);
    }
}
