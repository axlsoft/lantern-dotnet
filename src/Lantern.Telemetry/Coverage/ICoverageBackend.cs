namespace Lantern.Telemetry.Coverage;

/// <summary>
/// Abstracts the underlying coverage collection mechanism (Coverlet, custom profiler, etc.).
/// Swappable in tests and for future backend changes.
/// </summary>
public interface ICoverageBackend
{
    /// <summary>Called once at startup to initialise the backend.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a snapshot of current line-hit counts across all tracked files.
    /// Called before and after a test scope to compute a delta.
    /// </summary>
    Task<CoverageSnapshot> SnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>Called on shutdown to release resources.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
