using Lantern.Telemetry;

namespace SampleWorker;

/// <summary>
/// Demonstrates Lantern attribution in a background worker.
/// The pattern: read a test_id from an inbound message header, then wrap
/// message processing in <see cref="LanternScope.ForTest"/>.
///
/// See docs/recipes/ for concrete examples with MassTransit, NServiceBus,
/// Azure Service Bus, and RabbitMQ.
/// </summary>
public sealed class OrderProcessingWorker : BackgroundService
{
    private readonly ILogger<OrderProcessingWorker> _logger;

    public OrderProcessingWorker(ILogger<OrderProcessingWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderProcessingWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var (orderId, testId) = await DequeueAsync(stoppingToken);
            if (orderId is null)
                continue;

            // If this message was sent during a Playwright test, the test_id is
            // present in the message header. Wrap processing in a Lantern scope
            // so all code executed here is attributed to that test.
            if (testId is not null)
            {
                using var scope = LanternScope.ForTest(testId, testName: $"process-order:{orderId}");
                await ProcessOrderAsync(orderId, stoppingToken);
            }
            else
            {
                await ProcessOrderAsync(orderId, stoppingToken);
            }
        }
    }

    private static async Task<(string? OrderId, string? TestId)> DequeueAsync(
        CancellationToken cancellationToken)
    {
        // Stub: replace with your actual message bus consumer.
        // The test_id comes from a message property set by the Playwright plugin.
        // See docs/recipes/ for concrete header-reading patterns.
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        return (null, null);
    }

    private async Task ProcessOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing order {OrderId} (scope: {TestId})",
            orderId,
            LanternScope.Current?.TestId ?? "none");

        // Simulate work — coverage for everything called here is attributed to
        // the test that triggered this message, if a scope is active.
        await Task.Delay(50, cancellationToken);
    }
}
