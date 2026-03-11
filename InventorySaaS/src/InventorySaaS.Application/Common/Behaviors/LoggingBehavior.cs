using InventorySaaS.Domain.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InventorySaaS.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUserService;

    public LoggingBehavior(
        ILogger<LoggingBehavior<TRequest, TResponse>> logger,
        ICurrentUserService currentUserService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _currentUserService.UserId;
        var email = _currentUserService.Email ?? "Anonymous";

        _logger.LogInformation(
            "Handling {RequestName} for User {Email} (Id: {UserId})",
            requestName, email, userId);

        var response = await next();

        _logger.LogInformation(
            "Handled {RequestName} for User {Email} (Id: {UserId})",
            requestName, email, userId);

        return response;
    }
}
