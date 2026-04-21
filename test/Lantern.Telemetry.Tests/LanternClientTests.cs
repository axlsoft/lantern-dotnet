using Lantern.Telemetry;
using Xunit;

namespace Lantern.Telemetry.Tests;

public class LanternClientTests
{
    [Fact]
    public void Client_CanBeInstantiated()
    {
        var client = new LanternClient();
        Assert.NotNull(client);
    }
}
