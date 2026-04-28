using Lantern.Telemetry.Coverage;
using Xunit;

namespace Lantern.Telemetry.Tests;

public class CoverageSnapshotTests
{
    [Fact]
    public void DeltaTo_ReturnsOnlyNewHits()
    {
        var baseline = new CoverageSnapshot(new Dictionary<string, Dictionary<int, long>>
        {
            ["src/Foo.cs"] = new() { [10] = 1, [11] = 2 },
        });

        var later = new CoverageSnapshot(new Dictionary<string, Dictionary<int, long>>
        {
            ["src/Foo.cs"] = new() { [10] = 1, [11] = 3, [12] = 1 },
            ["src/Bar.cs"] = new() { [5] = 2 },
        });

        var delta = baseline.DeltaTo(later);

        // Line 10: no change → not in delta
        Assert.False(delta.Hits.TryGetValue("src/Foo.cs", out var fooLines) && fooLines.ContainsKey(10));

        // Line 11: +1 hit
        Assert.True(delta.Hits["src/Foo.cs"].ContainsKey(11));
        Assert.Equal(1L, delta.Hits["src/Foo.cs"][11]);

        // Line 12: new
        Assert.Equal(1L, delta.Hits["src/Foo.cs"][12]);

        // New file
        Assert.Equal(2L, delta.Hits["src/Bar.cs"][5]);
    }

    [Fact]
    public void DeltaTo_EmptyBaseline_ReturnsAllLaterHits()
    {
        var baseline = CoverageSnapshot.Empty;
        var later = new CoverageSnapshot(new Dictionary<string, Dictionary<int, long>>
        {
            ["src/X.cs"] = new() { [1] = 5 },
        });

        var delta = baseline.DeltaTo(later);
        Assert.Equal(5L, delta.Hits["src/X.cs"][1]);
    }

    [Fact]
    public void DeltaTo_NoNewHits_ReturnsEmptyDelta()
    {
        var snap = new CoverageSnapshot(new Dictionary<string, Dictionary<int, long>>
        {
            ["src/Y.cs"] = new() { [7] = 3 },
        });

        var delta = snap.DeltaTo(snap);
        Assert.Empty(delta.Hits);
    }
}
