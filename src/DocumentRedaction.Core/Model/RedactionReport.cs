using System.Collections.Frozen;

namespace DocumentRedaction.Core.Model;

/// <summary>Counts of redactions made, by kind. Contains no document content.</summary>
public sealed class RedactionReport
{
    public static RedactionReport Empty { get; } = new(FrozenDictionary<InformationKind, int>.Empty);

    public RedactionReport(IReadOnlyDictionary<InformationKind, int> countsByKind)
    {
        CountsByKind = countsByKind;
    }

    public IReadOnlyDictionary<InformationKind, int> CountsByKind { get; }

    public int Total => CountsByKind.Values.Sum();

    public static RedactionReport FromDetections(IEnumerable<Detection> detections) =>
        new(detections
            .GroupBy(detection => detection.Kind)
            .ToFrozenDictionary(group => group.Key, group => group.Count()));

    public RedactionReport Merge(RedactionReport other)
    {
        Dictionary<InformationKind, int> merged = new(CountsByKind);
        foreach ((InformationKind kind, int count) in other.CountsByKind)
        {
            merged[kind] = merged.GetValueOrDefault(kind) + count;
        }

        return new RedactionReport(merged.ToFrozenDictionary());
    }
}
