// Coverlet viability spike — T1.2-1
//
// PURPOSE: Validate that Coverlet injects per-assembly hit-count arrays accessible
// via reflection in a running process (not just at test-run exit).
//
// HOW TO RUN:
//   cd spikes/CoverletSpike
//   dotnet test /p:CollectCoverage=true
//
// WHAT TO LOOK FOR:
//   - Test output "PerTestSnapshots_Work" passes → per-test deltas confirmed.
//   - "No instrumented assemblies" → tracker type name pattern changed; update
//     CoverletCoverageBackend and re-run.
//
// Document results in docs/spikes/coverlet-viability.md and update ADR-012.

using System.Reflection;
using Xunit;
using Xunit.Abstractions;

public class CoverletViabilitySpike(ITestOutputHelper output)
{
    [Fact]
    public void PerTestSnapshots_Work()
    {
        output.WriteLine("=== Coverlet Viability Spike ===\n");

        // Step 1: Discover Coverlet tracker types across ALL loaded assemblies.
        // Coverlet 10.x may inject tracker types into the instrumented assembly or a bundled helper assembly.
        var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        output.WriteLine($"Loaded assemblies ({allAssemblies.Length}):");
        foreach (var asm in allAssemblies.Where(a => !a.IsDynamic))
            output.WriteLine($"  {asm.GetName().Name}");
        output.WriteLine("");

        // Coverlet 10.x injects tracker types into instrumented assemblies with:
        //   Namespace: "Coverlet.Core.Instrumentation.Tracker"
        //   Type name: "<AssemblyName>_<guid>"
        var trackerTypes = allAssemblies
            .Where(a => !a.IsDynamic)
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .Where(t => t.Namespace == "Coverlet.Core.Instrumentation.Tracker")
            .ToList();

        output.WriteLine($"Coverlet-related types found across all assemblies: {trackerTypes.Count}");
        foreach (var t in trackerTypes)
            output.WriteLine($"  {t.Assembly.GetName().Name} :: {t.FullName}");

        if (trackerTypes.Count == 0)
        {
            output.WriteLine("\nNo Coverlet tracker types found. Dumping all types from CoverletSpike assembly:");
            var spikeAssembly = allAssemblies.FirstOrDefault(a => a.GetName().Name == "CoverletSpike");
            if (spikeAssembly != null)
                foreach (var t in spikeAssembly.GetTypes())
                    output.WriteLine($"  {t.FullName}");
            output.WriteLine("\nRun with: dotnet test /p:CollectCoverage=true /p:IncludeTestAssembly=true");
            Assert.Fail("No Coverlet tracker types found — check type name pattern. See output above.");
            return;
        }

        output.WriteLine($"Found {trackerTypes.Count} Coverlet tracker type(s):");
        foreach (var t in trackerTypes) output.WriteLine($"  {t.FullName}");

        // Step 2: Inspect the tracker's fields to find the hit array.
        var tracker = trackerTypes[0];
        var fields = tracker.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        output.WriteLine("\nTracker fields:");
        foreach (var f in fields)
            output.WriteLine($"  [{(f.IsPublic ? "public" : "private")} static] {f.FieldType.Name} {f.Name}");

        // Step 3: Find the hits array.
        var hitsField = fields.FirstOrDefault(f =>
            f.Name is "HitsArray" or "Hits" or "_hits" or "hitCount"
            || f.FieldType == typeof(long[]) || f.FieldType == typeof(int[]));

        Assert.NotNull(hitsField);
        output.WriteLine($"\nHits field: {hitsField!.Name} ({hitsField.FieldType.Name})");

        // Step 4: Take a baseline snapshot.
        long[] Snapshot() => hitsField.GetValue(null) is long[] arr
            ? (long[])arr.Clone()
            : hitsField.GetValue(null) is int[] intArr
                ? intArr.Select(x => (long)x).ToArray()
                : Array.Empty<long>();

        var before = Snapshot();
        output.WriteLine($"Baseline hit-array length: {before.Length}");
        Assert.True(before.Length > 0, "Expected a non-empty hit array from the instrumented assembly.");

        // Step 5: Execute MethodA.
        MethodA();
        var afterA = Snapshot();
        var deltaA = afterA.Zip(before, (a, b) => a - b).Count(d => d > 0);
        output.WriteLine($"New hits after MethodA: {deltaA}");

        // Step 6: Execute MethodB.
        MethodB();
        var afterB = Snapshot();
        var deltaB = afterB.Zip(afterA, (a, b) => a - b).Count(d => d > 0);
        output.WriteLine($"New hits after MethodB (incremental): {deltaB}");

        Assert.True(deltaA > 0, "Expected MethodA to produce new hit increments.");
        Assert.True(deltaB > 0, "Expected MethodB to produce incremental hit increments distinct from MethodA.");

        output.WriteLine("\n✓ Per-test snapshots WORK. CoverletCoverageBackend reflection approach is viable.");
        output.WriteLine("  Tracker type name: " + tracker.FullName);
        output.WriteLine("  Hits field name:   " + hitsField.Name);
        output.WriteLine("  Hits field type:   " + hitsField.FieldType.Name);
        output.WriteLine("  Update ADR-012 with these values.");
    }

    private static void MethodA()
    {
        var x = 1 + 1;
        _ = x;
    }

    private static void MethodB()
    {
        var y = 2 * 3;
        _ = y;
    }
}
