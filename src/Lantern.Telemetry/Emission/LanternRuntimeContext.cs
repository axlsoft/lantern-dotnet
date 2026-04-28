namespace Lantern.Telemetry.Emission;

/// <summary>
/// Holds stable per-process identifiers used when building coverage batch payloads.
/// Generated once at startup; injected as a singleton dependency of <see cref="EventFlusher"/>.
/// </summary>
internal sealed class LanternRuntimeContext
{
    public string RunId { get; } = Guid.NewGuid().ToString("N");

    public string CommitSha { get; } =
        Environment.GetEnvironmentVariable("LANTERN_COMMIT_SHA")
        ?? Environment.GetEnvironmentVariable("GITHUB_SHA")
        ?? Environment.GetEnvironmentVariable("GIT_COMMIT_SHA")
        ?? "unknown";
}
