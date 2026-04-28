namespace Lantern.Telemetry.Coverage;

/// <summary>
/// Point-in-time snapshot of cumulative line hit counts.
/// Key: repo-relative file path. Value: map of line number → hit count.
/// </summary>
public sealed class CoverageSnapshot
{
    public static readonly CoverageSnapshot Empty = new(new Dictionary<string, Dictionary<int, long>>());

    public IReadOnlyDictionary<string, Dictionary<int, long>> Hits { get; }

    public CoverageSnapshot(Dictionary<string, Dictionary<int, long>> hits) => Hits = hits;

    /// <summary>
    /// Computes the delta between this snapshot (baseline) and a later snapshot.
    /// Returns only lines that were hit at least once more in <paramref name="later"/>.
    /// </summary>
    public CoverageSnapshot DeltaTo(CoverageSnapshot later)
    {
        var delta = new Dictionary<string, Dictionary<int, long>>();

        foreach (var (file, laterLines) in later.Hits)
        {
            Hits.TryGetValue(file, out var baseLines);

            foreach (var (line, laterCount) in laterLines)
            {
                var baseCount = baseLines is not null && baseLines.TryGetValue(line, out var b) ? b : 0;
                var diff = laterCount - baseCount;
                if (diff <= 0) continue;

                if (!delta.TryGetValue(file, out var fileMap))
                    delta[file] = fileMap = new Dictionary<int, long>();
                fileMap[line] = diff;
            }
        }

        return new CoverageSnapshot(delta);
    }
}
