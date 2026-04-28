namespace Lantern.Telemetry;

/// <summary>
/// Immutable context associated with a single test execution window.
/// </summary>
public sealed record LanternContext(
    string TestId,
    string? TestName,
    string? Suite,
    string? WorkerId);
