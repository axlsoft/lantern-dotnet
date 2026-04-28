using System.Diagnostics.Metrics;
using System.Net.Http.Headers;
using System.Threading.Channels;
using Google.Protobuf;
using Lantern.Telemetry.Proto.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProtoCoverage = Lantern.Telemetry.Proto.V1.Coverage;

namespace Lantern.Telemetry.Emission;

/// <summary>
/// Background loop that drains the event queue, batches events by <see cref="LanternOptions.BatchSize"/>
/// or <see cref="LanternOptions.FlushInterval"/>, and POSTs them to the collector.
/// </summary>
internal sealed class EventFlusher
{
    private static readonly Meter _meter = new("Lantern.Telemetry");
    private static readonly Counter<long> _flushed = _meter.CreateCounter<long>("lantern.events.flushed");
    private static readonly Counter<long> _failed = _meter.CreateCounter<long>("lantern.events.failed");

    private readonly CoverageEventQueue _queue;
    private readonly HttpClient _http;
    private readonly LanternOptions _options;
    private readonly ILogger<EventFlusher> _logger;
    private readonly string _runId;
    private readonly string _commitSha;

    public EventFlusher(
        CoverageEventQueue queue,
        HttpClient http,
        IOptions<LanternOptions> options,
        ILogger<EventFlusher> logger,
        LanternRuntimeContext runtimeContext)
    {
        _queue = queue;
        _http = http;
        _options = options.Value;
        _logger = logger;
        _runId = runtimeContext.RunId;
        _commitSha = runtimeContext.CommitSha;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var batch = new List<PendingCoverageEvent>(_options.BatchSize);
        using var timer = new PeriodicTimer(_options.FlushInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            var timerFired = false;
            var timerTask = timer.WaitForNextTickAsync(cancellationToken).AsTask();

            // Drain up to BatchSize events or until the timer fires.
            while (batch.Count < _options.BatchSize)
            {
                var readTask = _queue.Reader.ReadAsync(cancellationToken).AsTask();
                var completed = await Task.WhenAny(readTask, timerTask);

                if (completed == timerTask)
                {
                    timerFired = true;
                    break;
                }

                try
                {
                    batch.Add(await readTask);
                }
                catch (ChannelClosedException)
                {
                    // Queue completed — flush remaining and exit.
                    if (batch.Count > 0) await FlushBatchAsync(batch, CancellationToken.None);
                    return;
                }
                catch (OperationCanceledException)
                {
                    if (batch.Count > 0) await FlushBatchAsync(batch, CancellationToken.None);
                    return;
                }
            }

            if (batch.Count > 0 || timerFired)
            {
                if (batch.Count > 0) await FlushBatchAsync(batch, cancellationToken);
                batch.Clear();
            }
        }
    }

    private async Task FlushBatchAsync(List<PendingCoverageEvent> batch, CancellationToken cancellationToken)
    {
        var proto = BuildProto(batch);
        var bytes = proto.ToByteArray();

        try
        {
            using var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
            var response = await _http.PostAsync("/v1/coverage", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            _flushed.Add(batch.Count);
            _logger.LogDebug("Flushed {Count} coverage events", batch.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _failed.Add(batch.Count);
            _logger.LogWarning(ex, "Failed to flush {Count} coverage events; events dropped", batch.Count);
        }
    }

    private CoverageBatch BuildProto(List<PendingCoverageEvent> batch)
    {
        var pb = new CoverageBatch
        {
            BatchId = Guid.NewGuid().ToString(),
            Resource = new Resource
            {
                ProjectId = _options.ProjectId,
                RunId = _runId,
                CommitSha = _commitSha,
                SdkName = "lantern-dotnet",
                SdkVersion = typeof(EventFlusher).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                SchemaVersion = "1",
            },
        };

        foreach (var ev in batch)
        {
            pb.Events.Add(new ProtoCoverage
            {
                TestId = ev.TestId,
                FilePath = ev.FilePath,
                LineStart = ev.LineStart,
                LineEnd = ev.LineEnd,
                HitCount = ev.HitCount,
                AttributionMode = AttributionMode.Serialized,
            });
        }

        return pb;
    }
}
