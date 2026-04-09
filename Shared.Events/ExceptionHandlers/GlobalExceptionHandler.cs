using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Events.Exceptions;

namespace Shared.Events.ExceptionHandlers;

/// <summary>
/// Handler #2 in the chain (catch-all).
/// Handles <see cref="NotFoundException"/> as HTTP 404 and every other unhandled
/// exception as HTTP 500 Internal Server Error — both formatted as RFC 7807 Problem Details.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Map known domain exceptions to appropriate HTTP status codes
        var (statusCode, title) = exception switch
        {
            NotFoundException           => (StatusCodes.Status404NotFound,       "Resource Not Found"),
            ConflictException           => (StatusCodes.Status409Conflict,       "Conflict"),
            AuthException               => (StatusCodes.Status401Unauthorized,   "Unauthorized"),
            ValidationException         => (StatusCodes.Status400BadRequest,     "Validation Error"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden,      "Access Denied"),
            _                           => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        // Only log as Error for true 5xx surprises
        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Domain exception {ExceptionType} on {Method} {Path}",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        var problemDetails = new ProblemDetails
        {
            Type     = "https://tools.ietf.org/html/rfc7807",
            Title    = title,
            Status   = statusCode,
            Detail   = exception.Message,
            Instance = httpContext.Request.Path
        };

        // Always attach TraceId for log correlation in production
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true; // ✅ Always handles — end of chain
    }
}
