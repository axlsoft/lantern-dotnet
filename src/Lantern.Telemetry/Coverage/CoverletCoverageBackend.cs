using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Lantern.Telemetry.Coverage;

/// <summary>
/// Coverage backend that reads hit counters from Coverlet-instrumented assemblies via reflection.
///
/// PREREQUISITES — the target app must be built with Coverlet instrumentation:
///   Add to the app's .csproj: &lt;PackageReference Include="coverlet.msbuild" Version="10.*" /&gt;
///   Set build property:       &lt;CollectCoverage&gt;true&lt;/CollectCoverage&gt;
///   For the test assembly:    &lt;IncludeTestAssembly&gt;true&lt;/IncludeTestAssembly&gt;
///
/// HOW IT WORKS — Coverlet 10.x instruments each assembly during the build and injects
/// a static tracker class with namespace "Coverlet.Core.Instrumentation.Tracker" and
/// class name "&lt;AssemblyName&gt;_&lt;guid&gt;". The tracker exposes an Int32[] HitsArray
/// that Coverlet increments for each executed sequence point.
///
/// Lantern clones this array before and after each test scope to compute per-test deltas.
/// The index→(file,line) mapping lives in Coverlet's InstrumenterState file (a build
/// artifact). Phase 2 will read InstrumenterState to emit proper file/line coverage;
/// for now we emit (assemblyPath, hitIndex, count) so all data is captured.
///
/// Validated against coverlet.msbuild 10.0.0 — see docs/spikes/coverlet-viability.md.
/// </summary>
internal sealed class CoverletCoverageBackend : ICoverageBackend
{
    // Coverlet 10.x injects tracker types in this namespace.
    // Class name: "<AssemblyName>_<guid>" (one per instrumented assembly module).
    private const string TrackerNamespace = "Coverlet.Core.Instrumentation.Tracker";
    private const string HitsFieldName = "HitsArray";
    private const string HitsFilePathFieldName = "HitsFilePath";

    private readonly ILogger<CoverletCoverageBackend> _logger;

    // Maps each discovered tracker to its HitsArray field.
    private readonly List<(string AssemblyName, FieldInfo Hits)> _trackers = [];

    public CoverletCoverageBackend(ILogger<CoverletCoverageBackend> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _trackers.Clear();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic) continue;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.OfType<Type>().ToArray();
            }

            foreach (var type in types)
            {
                if (type.Namespace != TrackerNamespace) continue;

                var hitsField = type.GetField(HitsFieldName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (hitsField?.FieldType != typeof(int[])) continue;

                _trackers.Add((assembly.GetName().Name ?? type.Name, hitsField));
                _logger.LogDebug("Found Coverlet tracker in {Assembly}: {Type}", assembly.GetName().Name, type.FullName);
            }
        }

        if (_trackers.Count == 0)
            _logger.LogWarning(
                "CoverletCoverageBackend: no instrumented assemblies found. " +
                "Build the target with coverlet.msbuild, CollectCoverage=true, " +
                "and IncludeTestAssembly=true if coverage of the SUT in the test process is needed.");

        return Task.CompletedTask;
    }

    public Task<CoverageSnapshot> SnapshotAsync(CancellationToken cancellationToken = default)
    {
        var hits = new Dictionary<string, Dictionary<int, long>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (assemblyName, hitsField) in _trackers)
        {
            try
            {
                if (hitsField.GetValue(null) is not int[] rawHits) continue;

                // Key = assembly name (Phase 2 will map indices to file/line via InstrumenterState).
                // Line = hit index; this is a placeholder until proper source mapping is implemented.
                var lineMap = new Dictionary<int, long>(rawHits.Length);
                for (int i = 0; i < rawHits.Length; i++)
                {
                    if (rawHits[i] > 0)
                        lineMap[i] = rawHits[i];
                }

                if (lineMap.Count > 0)
                    hits[assemblyName] = lineMap;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read coverage from tracker for assembly {Assembly}", assemblyName);
            }
        }

        return Task.FromResult(new CoverageSnapshot(hits));
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _trackers.Clear();
        return Task.CompletedTask;
    }
}
