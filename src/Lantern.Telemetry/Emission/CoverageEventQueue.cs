using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lantern.Telemetry.Emission;

internal sealed record PendingCoverageEvent(
    string TestId,
    string FilePath,
    int LineStart,
    int LineEnd,
    int HitCount);

internal sealed class CoverageEventQueue : IDisposable
{
    private static readonly Meter _meter = new("Lantern.Telemetry");
    private static readonly Counter<long> _queued = _meter.CreateCounter<long>("lantern.events.queued");
    private static readonly Counter<long> _dropped = _meter.CreateCounter<long>("lantern.events.dropped");

    private readonly Channel<PendingCoverageEvent> _channel;
    private readonly LanternOptions _options;
    private readonly ILogger<CoverageEventQueue> _logger;

    public CoverageEventQueue(IOptions<LanternOptions> options, ILogger<CoverageEventQueue> logger)
    {
        _options = options.Value;
        _logger = logger;
        _channel = Channel.CreateBounded<PendingCoverageEvent>(new BoundedChannelOptions(_options.BufferCapacity)
        {
            FullMode = _options.DropPolicy == BufferDropPolicy.DropOldest
                ? BoundedChannelFullMode.DropOldest
                : BoundedChannelFullMode.DropNewest,
            SingleWriter = false,
            SingleReader = true,
        });
    }

    public ChannelReader<PendingCoverageEvent> Reader => _channel.Reader;

    public void Enqueue(PendingCoverageEvent evt)
    {
        if (!_channel.Writer.TryWrite(evt))
        {
            _dropped.Add(1);
            _logger.LogDebug("Coverage event dropped (buffer full, policy={Policy})", _options.DropPolicy);
            return;
        }
        _queued.Add(1);
    }

    public void Complete() => _channel.Writer.Complete();

    public void Dispose() => Complete();
}
