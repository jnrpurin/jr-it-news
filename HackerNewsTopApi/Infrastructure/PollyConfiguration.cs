using Polly;
using Polly.Extensions.Http;
using Polly.CircuitBreaker;

namespace HackerNewsTopApi.Infrastructure
{
    public static class PollyConfiguration
    {
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError() // 5xx e 408 (Timeout)
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests) // 429
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var logger = context.GetLogger();
                        logger?.LogWarning(
                            "Attempt {RetryCount} after {Delay}s because the: {Result}",
                            retryCount,
                            timespan.TotalSeconds,
                            outcome.Result?.StatusCode.ToString() ?? outcome.Exception?.Message ?? "Unknown"
                        );
                    }
                );
        }

        /// <summary>
        /// Circuit Breaker Policy
        /// Opens the circuit after 5 consecutive faults
        /// Remains open for 30 seconds before retrying (half-open)
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5, // Nro of failures before open
                    durationOfBreak: TimeSpan.FromSeconds(30), // Time stys open
                    onBreak: (outcome, breakDelay, context) =>
                    {
                        var logger = context.GetLogger();
                        logger?.LogError(
                            "The circuit breaker was OPENED for {Delay}s later 5 consecutive faults. Last fault: {Result}",
                            breakDelay.TotalSeconds,
                            outcome.Result?.StatusCode.ToString() ?? outcome.Exception?.Message ?? "Unknown"
                        );
                    },
                    onReset: (context) =>
                    {
                        var logger = context.GetLogger();
                        logger?.LogInformation("Circuit Breaker RESETED. Connections were normalizeds.");
                    },
                    onHalfOpen: () =>
                    {
                        Console.WriteLine("Circuit Breaker HALF-OPEN. Connection test...");
                    }
                );
        }

        /// <summary>
        /// Timeout policy
        /// Cancel requests longer then 8 seconds
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(
                timeout: TimeSpan.FromSeconds(8),
                onTimeoutAsync: (context, timespan, task) =>
                {
                    var logger = context.GetLogger();
                    logger?.LogWarning("Request canceled, timeout after {Timeout}s", timespan.TotalSeconds);
                    return Task.CompletedTask;
                }
            );
        }

        private static ILogger? GetLogger(this Context context)
        {
            if (context.TryGetValue("Logger", out var logger))
            {
                return logger as ILogger;
            }
            return null;
        }
    }
}