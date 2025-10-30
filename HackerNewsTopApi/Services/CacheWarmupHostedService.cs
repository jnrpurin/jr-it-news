using HackerNewsTopApi.Services.Interfaces;

namespace HackerNewsTopApi.Services
{
    /// <summary>
    /// Background service that keeps the cache always warm
    /// Updates automatically every 2 minutes
    /// </summary>
    public class CacheWarmupHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CacheWarmupHostedService> _logger;
        private const int WarmupIntervalMinutes = 2;

        public CacheWarmupHostedService(
            IServiceProvider serviceProvider,
            ILogger<CacheWarmupHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Cache Warmup Service started");

            // Wait 10 seconds for the application to finish initializing
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            // First immediate execution
            await WarmupCacheAsync(stoppingToken);

            // Infinite loop executing every N minutes
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(WarmupIntervalMinutes), stoppingToken);
                    await WarmupCacheAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Cache Warmup Service shutting down...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Cache Warmup Service loop");
                    // Wait 30s before retrying in case of error
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }

            _logger.LogInformation("Cache Warmup Service stopped");
        }

        private async Task WarmupCacheAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var hackerNewsService = scope.ServiceProvider.GetRequiredService<IHackerNewsService>();

            try
            {
                _logger.LogInformation("Executing automatic cache warmup...");
                await hackerNewsService.WarmupCacheAsync(stoppingToken);
                _logger.LogInformation("Automatic warmup completed. Next execution in {Minutes} minutes", WarmupIntervalMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automatic cache warmup failed");
            }
        }
    }
}