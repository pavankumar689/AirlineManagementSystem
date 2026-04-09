using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Events.Exceptions;

namespace Shared.Events.ExceptionHandlers;

/// <summary>
/// Handler #1 in the chain.
/// Catches <see cref="Exceptions.ValidationException"/> and returns HTTP 400 Bad Request
/// formatted as RFC 7807 Problem Details.
/// </summary>
public sealed class ValidationExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ValidationExceptionHandler> _logger;

    public ValidationExceptionHandler(ILogger<ValidationExceptionHandler> logger)
        => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Only handle our custom ValidationException — pass everything else down the chain
        if (exception is not Exceptions.ValidationException validationException)
            return false;

        _logger.LogWarning(
            exception,
            "Validation error on {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var problemDetails = new ValidationProblemDetails(
            validationException.Errors.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value))
        {
            Type    = "https://tools.ietf.org/html/rfc7807",
            Title   = "Validation Failed",
            Status  = StatusCodes.Status400BadRequest,
            Detail  = validationException.Message,
            Instance = httpContext.Request.Path
        };

        // Attach the trace ID so developers can correlate with logs
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true; // ✅ Handled — stop the chain here
    }
}
