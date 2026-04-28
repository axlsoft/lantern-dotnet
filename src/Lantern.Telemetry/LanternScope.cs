using System.Diagnostics;

namespace Lantern.Telemetry;

/// <summary>
/// Establishes and restores Lantern test context for the current async call chain.
/// </summary>
public static class LanternScope
{
    private static readonly AsyncLocal<LanternContext?> _current = new();

    /// <summary>Gets the active context, or null when no scope is active.</summary>
    public static LanternContext? Current => _current.Value;

    /// <summary>
    /// Creates a scope for the given test. Disposes restores the previous context.
    /// Also sets Activity baggage for OTel interop (per ADR-013).
    /// </summary>
    public static IDisposable ForTest(
        string testId,
        string? testName = null,
        string? suite = null,
        string? workerId = null)
    {
        if (string.IsNullOrWhiteSpace(testId))
            throw new ArgumentException("testId must not be empty.", nameof(testId));

        var previous = _current.Value;
        var ctx = new LanternContext(testId, testName, suite, workerId);
        _current.Value = ctx;

        Activity.Current?.SetBaggage("lantern.test_id", testId);
        if (testName is not null)
            Activity.Current?.SetBaggage("lantern.test_name", testName);

        return new ScopeHandle(previous);
    }

    internal static void Set(LanternContext? ctx) => _current.Value = ctx;

    private sealed class ScopeHandle(LanternContext? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _current.Value = previous;
        }
    }
}
