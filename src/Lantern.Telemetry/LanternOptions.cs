using System.ComponentModel.DataAnnotations;

namespace Lantern.Telemetry;

public sealed class LanternOptions
{
    [Required(AllowEmptyStrings = false)]
    public string CollectorEndpoint { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string ApiKey { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string ProjectId { get; set; } = "";

    public bool Enabled { get; set; } = true;

    [Range(1, 10_000)]
    public int BatchSize { get; set; } = 100;

    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);

    [Range(100, 1_000_000)]
    public int BufferCapacity { get; set; } = 10_000;

    public BufferDropPolicy DropPolicy { get; set; } = BufferDropPolicy.DropOldest;

    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public string ControlPlanePath { get; set; } = "/_lantern";

    public bool ControlPlaneEnabled { get; set; } = true;

    public string RepoRoot { get; set; } = "";
}

public enum BufferDropPolicy
{
    DropOldest,
    DropNewest,
}
