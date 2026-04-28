using Lantern.Telemetry.Coverage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Lantern.Telemetry.Tests;

public class CoverageManagerTests
{
    private static CoverageManager BuildManager(ICoverageBackend backend) =>
        new(backend, Options.Create(new LanternOptions
        {
            CollectorEndpoint = "http://localhost:8080",
            ApiKey = "lntn_test",
            ProjectId = "proj-1",
        }), NullLogger<CoverageManager>.Instance);

    [Fact]
    public async Task EndTestScope_WithNoMatchingBegin_IsIgnored()
    {
        var backend = new FakeCoverageBackend();
        var manager = BuildManager(backend);
        // Should not throw
        await manager.EndTestScopeAsync("nonexistent");
    }

    [Fact]
    public async Task BeginThenEnd_ProducesDelta()
    {
        var backend = new FakeCoverageBackend();
        var manager = BuildManager(backend);

        CoverageSnapshot? received = null;
        manager.CoverageReady += (_, snap) => { received = snap; return Task.CompletedTask; };

        await manager.InitialiseAsync();
        await manager.BeginTestScopeAsync("test-1");

        // Simulate code executing: add hits in the backend
        backend.AddHit("src/Foo.cs", 10, 2);
        backend.AddHit("src/Foo.cs", 11, 1);

        await manager.EndTestScopeAsync("test-1");

        Assert.NotNull(received);
        Assert.True(received!.Hits.ContainsKey("src/Foo.cs"));
        Assert.Equal(2L, received.Hits["src/Foo.cs"][10]);
        Assert.Equal(1L, received.Hits["src/Foo.cs"][11]);
    }

    [Fact]
    public async Task TwoSequentialScopes_ProduceNonOverlappingDeltas()
    {
        var backend = new FakeCoverageBackend();
        var manager = BuildManager(backend);

        var deltas = new List<CoverageSnapshot>();
        manager.CoverageReady += (_, snap) => { deltas.Add(snap); return Task.CompletedTask; };

        await manager.InitialiseAsync();

        // Scope 1
        await manager.BeginTestScopeAsync("test-a");
        backend.AddHit("src/A.cs", 5, 3);
        await manager.EndTestScopeAsync("test-a");

        // Scope 2
        await manager.BeginTestScopeAsync("test-b");
        backend.AddHit("src/B.cs", 20, 1);
        await manager.EndTestScopeAsync("test-b");

        Assert.Equal(2, deltas.Count);
        Assert.True(deltas[0].Hits.ContainsKey("src/A.cs"));
        Assert.False(deltas[0].Hits.ContainsKey("src/B.cs"));
        Assert.True(deltas[1].Hits.ContainsKey("src/B.cs"));
    }

    [Fact]
    public async Task ConcurrentBeginScope_IsRejectedWithWarning()
    {
        var backend = new FakeCoverageBackend();
        var manager = BuildManager(backend);

        int readyCount = 0;
        manager.CoverageReady += (_, _) => { readyCount++; return Task.CompletedTask; };

        await manager.InitialiseAsync();
        await manager.BeginTestScopeAsync("test-1");
        await manager.BeginTestScopeAsync("test-2"); // should be ignored

        backend.AddHit("src/X.cs", 1, 5);

        await manager.EndTestScopeAsync("test-1");
        Assert.Equal(1, readyCount);
    }

    // ── Fake backend ──────────────────────────────────────────────────────────

    private sealed class FakeCoverageBackend : ICoverageBackend
    {
        private readonly Dictionary<string, Dictionary<int, long>> _hits = new();

        public void AddHit(string file, int line, long count)
        {
            if (!_hits.TryGetValue(file, out var lines))
                _hits[file] = lines = new();

            lines[line] = lines.TryGetValue(line, out var existing) ? existing + count : count;
        }

        public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<CoverageSnapshot> SnapshotAsync(CancellationToken ct = default) =>
            Task.FromResult(new CoverageSnapshot(
                _hits.ToDictionary(k => k.Key, v => new Dictionary<int, long>(v.Value))));

        public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
