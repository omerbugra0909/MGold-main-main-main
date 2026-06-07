using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Hubs;

namespace MGold.Application.Services;

public class MarketRefreshWorker(
    IServiceScopeFactory scopeFactory,
    IHubContext<MarketHub> hubContext,
    IOptions<MarketDataSettings> settings,
    ILogger<MarketRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await DelaySafelyAsync(TimeSpan.FromSeconds(4), stoppingToken))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IMarketDataService>();
                var result = await service.RefreshAsync(cancellationToken: stoppingToken);

                if (result.Succeeded)
                {
                    await hubContext.Clients.All.SendAsync("market:pulse", new MarketPulseDto
                    {
                        LastUpdatedAt = result.LastUpdatedAt,
                        ProviderKey = result.ProviderKey,
                        UsedFallback = result.UsedFallback,
                        Status = result.UsedFallback ? "recentSnapshot" : "active/realtime"
                    }, CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Market refresh worker is stopping because application shutdown was requested.");
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Market refresh worker iteration failed.");
            }

            if (!await DelaySafelyAsync(TimeSpan.FromSeconds(settings.Value.DefaultRefreshIntervalSeconds), stoppingToken))
            {
                break;
            }
        }
    }

    private static async Task<bool> DelaySafelyAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
