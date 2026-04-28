using Microsoft.Extensions.Options;
using Xunit;

namespace Lantern.Telemetry.Tests;

public class LanternOptionsTests
{
    private static ValidateOptionsResult Validate(LanternOptions opts) =>
        new LanternOptionsValidator().Validate(null, opts);

    private static LanternOptions Valid() => new()
    {
        CollectorEndpoint = "http://collector:8080",
        ApiKey = "lntn_testkey",
        ProjectId = "proj-123",
    };

    [Fact]
    public void ValidOptions_PassValidation()
    {
        var result = Validate(Valid());
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void MissingCollectorEndpoint_FailsValidation()
    {
        var opts = Valid(); opts.CollectorEndpoint = "";
        var result = Validate(opts);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void InvalidUri_FailsValidation()
    {
        var opts = Valid(); opts.CollectorEndpoint = "not-a-uri";
        var result = Validate(opts);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void MissingApiKey_FailsValidation()
    {
        var opts = Valid(); opts.ApiKey = "";
        var result = Validate(opts);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void MissingProjectId_FailsValidation()
    {
        var opts = Valid(); opts.ProjectId = "";
        var result = Validate(opts);
        Assert.False(result.Succeeded);
    }
}
