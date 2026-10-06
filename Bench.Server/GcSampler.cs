using Microsoft.Extensions.Hosting;

namespace Bench;

sealed class GcSampler : BackgroundService {
    static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var interval = DefaultInterval;

        try {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(stoppingToken)) {
                GcReport.Print($"tick-{DateTime.UtcNow}");
            }
        }
        catch (OperationCanceledException) {
            
        }
    }

}