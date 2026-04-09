using System.Net;
using Polly;
using Polly.Retry;

namespace ApiGateway.Handlers;

/// <summary>
/// A Polly-powered Delegating Handler that intercepts all outgoing Ocelot traffic
/// and automatically retries the downstream microservice if it is temporarily offline.
/// </summary>
public class RetryDelegatingHandler : DelegatingHandler
{
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public RetryDelegatingHandler(ILogger<RetryDelegatingHandler> logger)
    {
        // Define a Polly Retry Policy:
        // Handle network exceptions OR HTTP 5xx Server Errors
        // Retry 3 times, with an exponential backoff (2s, 4s, 8s)
        _retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(msg => msg.StatusCode == HttpStatusCode.ServiceUnavailable 
                          || msg.StatusCode == HttpStatusCode.InternalServerError
                          || msg.StatusCode == HttpStatusCode.BadGateway)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(retryAttempt * 2),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    var uri = outcome.Result?.RequestMessage?.RequestUri?.ToString() ?? "Unknown URI";
                    logger.LogWarning($"[POLLY] Downstream service {uri} did not respond. Retrying attempt {retryAttempt} in {timespan.TotalSeconds} seconds...");
                });
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Execute the HTTP Request wrapped inside the Polly Policy
        return _retryPolicy.ExecuteAsync(() => base.SendAsync(request, cancellationToken));
    }
}
