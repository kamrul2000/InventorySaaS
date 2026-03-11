using System.Net;
using System.Text.Json;
using FluentValidation;
using InventorySaaS.Domain.Exceptions;

namespace InventorySaaS.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString();

        var (statusCode, response) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                new ProblemResponse
                {
                    Type = "ValidationError",
                    Title = "One or more validation errors occurred.",
                    Status = (int)HttpStatusCode.BadRequest,
                    Errors = validationEx.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
                    CorrelationId = correlationId
                }),

            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                new ProblemResponse
                {
                    Type = "NotFound",
                    Title = notFoundEx.Message,
                    Status = (int)HttpStatusCode.NotFound,
                    CorrelationId = correlationId
                }),

            ConflictException conflictEx => (
                HttpStatusCode.Conflict,
                new ProblemResponse
                {
                    Type = "Conflict",
                    Title = conflictEx.Message,
                    Status = (int)HttpStatusCode.Conflict,
                    CorrelationId = correlationId
                }),

            ForbiddenAccessException forbiddenEx => (
                HttpStatusCode.Forbidden,
                new ProblemResponse
                {
                    Type = "Forbidden",
                    Title = forbiddenEx.Message,
                    Status = (int)HttpStatusCode.Forbidden,
                    CorrelationId = correlationId
                }),

            BadRequestException badReqEx => (
                HttpStatusCode.BadRequest,
                new ProblemResponse
                {
                    Type = "BadRequest",
                    Title = badReqEx.Message,
                    Status = (int)HttpStatusCode.BadRequest,
                    CorrelationId = correlationId
                }),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                new ProblemResponse
                {
                    Type = "Unauthorized",
                    Title = "Authentication is required.",
                    Status = (int)HttpStatusCode.Unauthorized,
                    CorrelationId = correlationId
                }),

            _ => (
                HttpStatusCode.InternalServerError,
                new ProblemResponse
                {
                    Type = "InternalServerError",
                    Title = "An unexpected error occurred.",
                    Status = (int)HttpStatusCode.InternalServerError,
                    CorrelationId = correlationId
                })
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);
        }
        else
        {
            _logger.LogWarning("Handled exception: {ExceptionType} - {Message}. CorrelationId: {CorrelationId}",
                exception.GetType().Name, exception.Message, correlationId);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}

public class ProblemResponse
{
    public string Type { get; set; } = default!;
    public string Title { get; set; } = default!;
    public int Status { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
    public string? CorrelationId { get; set; }
}
