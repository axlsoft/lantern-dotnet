using Lantern.Telemetry.Coverage;
using Lantern.Telemetry.Emission;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lantern.Telemetry.AspNetCore;

/// <summary>
/// Initialises the coverage backend on startup, runs the event flusher loop,
/// and drains the queue on graceful shutdown.
/// </summary>
internal sealed class LanternBackgroundService : BackgroundService
{
    private readonly CoverageManager _coverage;
    private readonly CoverageEventQueue _queue;
    private readonly EventFlusher _flusher;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly LanternOptions _options;
    private readonly ILogger<LanternBackgroundService> _logger;

    public LanternBackgroundService(
        CoverageManager coverage,
        CoverageEventQueue queue,
        EventFlusher flusher,
        IHostApplicationLifetime lifetime,
        IOptions<LanternOptions> options,
        ILogger<LanternBackgroundService> logger)
    {
        _coverage = coverage;
        _queue = queue;
        _flusher = flusher;
        _lifetime = lifetime;
        _options = options.Value;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return;
        await _coverage.InitialiseAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;

        _logger.LogInformation("Lantern event flusher started.");

        // Tie our flush loop to the application stopping signal so we get a
        // bounded grace period to drain rather than a hard kill.
        using var gracePeriodCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _lifetime.ApplicationStopping.Register(() =>
        {
            Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ =>
            {
                if (!gracePeriodCts.IsCancellationRequested)
                    gracePeriodCts.Cancel();
            }, TaskScheduler.Default);
        });

        await _flusher.RunAsync(gracePeriodCts.Token);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return;
        _queue.Complete();
        await _coverage.ShutdownAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
