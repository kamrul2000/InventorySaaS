using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Interfaces;
using MediatR;

namespace InventorySaaS.Application.Features.Auth.Commands;

public record RevokeTokenCommand(string RefreshToken, string? IpAddress) : IRequest<Result>;

public class RevokeTokenCommandHandler : IRequestHandler<RevokeTokenCommand, Result>
{
    private readonly ITokenService _tokenService;

    public RevokeTokenCommandHandler(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public async Task<Result> Handle(RevokeTokenCommand request, CancellationToken cancellationToken)
    {
        await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken, request.IpAddress);
        return Result.Success();
    }
}
