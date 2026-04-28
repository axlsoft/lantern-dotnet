using Lantern.Telemetry.Coverage;
using Lantern.Telemetry.Emission;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lantern.Telemetry;

/// <summary>
/// Injectable facade for programmatic Lantern control in non-middleware scenarios
/// (e.g. IHostedService workers, console apps). For ASP.NET Core, prefer
/// <c>UseLanternTelemetry()</c> which wires this up automatically.
/// </summary>
public sealed class LanternClient
{
    private readonly CoverageManager _coverage;
    private readonly CoverageEventQueue _queue;
    private readonly LanternOptions _options;
    private readonly ILogger<LanternClient> _logger;

    internal LanternClient(
        CoverageManager coverage,
        CoverageEventQueue queue,
        IOptions<LanternOptions> options,
        ILogger<LanternClient> logger)
    {
        _coverage = coverage;
        _queue = queue;
        _options = options.Value;
        _logger = logger;

        _coverage.CoverageReady += OnCoverageReady;
    }

    public bool IsEnabled => _options.Enabled;

    /// <summary>
    /// Begins a Lantern test scope. Returns a disposable that ends the scope on dispose.
    /// </summary>
    public async Task<IAsyncDisposable> BeginTestScopeAsync(
        string testId,
        string? testName = null,
        string? suite = null,
        string? workerId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return NullScope.Instance;

        LanternScope.Set(new LanternContext(testId, testName, suite, workerId));
        await _coverage.BeginTestScopeAsync(testId, cancellationToken);
        return new TestScopeHandle(testId, this);
    }

    internal async Task EndTestScopeAsync(string testId, CancellationToken cancellationToken = default)
    {
        LanternScope.Set(null);
        await _coverage.EndTestScopeAsync(testId, cancellationToken);
    }

    private Task OnCoverageReady(string testId, CoverageSnapshot snapshot)
    {
        foreach (var (file, lines) in snapshot.Hits)
        {
            if (lines.Count == 0) continue;

            int lineStart = int.MaxValue, lineEnd = int.MinValue;
            long maxHit = 0;

            foreach (var (line, hit) in lines)
            {
                lineStart = Math.Min(lineStart, line);
                lineEnd = Math.Max(lineEnd, line);
                maxHit = Math.Max(maxHit, hit);
            }

            _queue.Enqueue(new PendingCoverageEvent(
                testId, file, lineStart, lineEnd, (int)Math.Min(maxHit, int.MaxValue)));
        }

        return Task.CompletedTask;
    }

    private sealed class TestScopeHandle(string testId, LanternClient client) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() =>
            await client.EndTestScopeAsync(testId);
    }

    private sealed class NullScope : IAsyncDisposable
    {
        public static readonly NullScope Instance = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
