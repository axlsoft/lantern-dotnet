using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lantern.Telemetry.Coverage;

/// <summary>
/// Owns the active coverage session. Captures a baseline snapshot when a test scope begins,
/// computes the delta on end, and hands it to the emission pipeline.
/// </summary>
public sealed class CoverageManager
{
    private readonly ICoverageBackend _backend;
    private readonly ILogger<CoverageManager> _logger;
    private readonly LanternOptions _options;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _activeTestId;
    private CoverageSnapshot? _baseline;

    public event Func<string, CoverageSnapshot, Task>? CoverageReady;

    public CoverageManager(
        ICoverageBackend backend,
        IOptions<LanternOptions> options,
        ILogger<CoverageManager> logger)
    {
        _backend = backend;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitialiseAsync(CancellationToken cancellationToken = default) =>
        await _backend.StartAsync(cancellationToken);

    public async Task ShutdownAsync(CancellationToken cancellationToken = default) =>
        await _backend.StopAsync(cancellationToken);

    /// <summary>
    /// Begins a coverage window for <paramref name="testId"/>.
    /// Rejects concurrent scopes — per ADR-012, MVP uses SERIALIZED attribution only.
    /// </summary>
    public async Task BeginTestScopeAsync(string testId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_activeTestId is not null)
            {
                _logger.LogWarning(
                    "BeginTestScope called for {NewTest} while {ActiveTest} is still active. " +
                    "Ignoring — serialized attribution requires sequential test scopes.",
                    testId, _activeTestId);
                return;
            }

            _baseline = await _backend.SnapshotAsync(cancellationToken);
            _activeTestId = testId;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Closes the coverage window, computes the delta, and fires <see cref="CoverageReady"/>.
    /// </summary>
    public async Task EndTestScopeAsync(string testId, CancellationToken cancellationToken = default)
    {
        CoverageSnapshot? delta = null;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_activeTestId != testId)
            {
                _logger.LogWarning(
                    "EndTestScope called for {TestId} but active scope is {ActiveTest}. Ignoring.",
                    testId, _activeTestId ?? "(none)");
                return;
            }

            var current = await _backend.SnapshotAsync(cancellationToken);
            delta = _baseline!.DeltaTo(current);
            _activeTestId = null;
            _baseline = null;
        }
        finally
        {
            _lock.Release();
        }

        if (delta is not null && CoverageReady is not null)
            await CoverageReady.Invoke(testId, delta);
    }
}
