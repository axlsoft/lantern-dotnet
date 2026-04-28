using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lantern.Telemetry.Hosting;

/// <summary>
/// Adapter for IHostedService background workers that need Lantern coverage attribution.
/// Workers use <see cref="LanternScope.ForTest"/> (or inject <see cref="LanternClient"/>)
/// to establish coverage windows around message processing.
///
/// See docs/recipes/ for patterns extracting test IDs from common message-bus headers
/// (MassTransit, NServiceBus, Azure Service Bus, RabbitMQ).
///
/// Timer-triggered workers without inbound messages are not instrumented in MVP (documented
/// limitation — see SDK README).
/// </summary>
public sealed class LanternHostedService : IHostedService
{
    private readonly LanternClient _client;
    private readonly ILogger<LanternHostedService> _logger;

    public LanternHostedService(LanternClient client, ILogger<LanternHostedService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_client.IsEnabled)
            _logger.LogInformation("Lantern telemetry is disabled; coverage will not be collected.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
