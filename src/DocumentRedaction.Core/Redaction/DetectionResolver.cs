using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Redaction;

/// <summary>
/// Turns overlapping candidate detections into a non-overlapping set. Longer spans win;
/// on equal length the kind with the lower <see cref="InformationKindInfo.Priority"/> wins;
/// on a full tie the earlier span wins.
/// </summary>
public static class DetectionResolver
{
    public static IReadOnlyList<Detection> Resolve(IEnumerable<Detection> candidates)
    {
        List<Detection> ordered = candidates
            .OrderByDescending(detection => detection.Length)
            .ThenBy(detection => InformationKinds.Get(detection.Kind).Priority)
            .ThenBy(detection => detection.Start)
            .ToList();

        // Accepted detections kept sorted by Start so overlap checks only touch the neighbours.
        List<Detection> accepted = [];
        foreach (Detection candidate in ordered)
        {
            int index = InsertionIndex(accepted, candidate.Start);
            bool clashesBefore = index > 0 && accepted[index - 1].Overlaps(candidate);
            bool clashesAfter = index < accepted.Count && accepted[index].Overlaps(candidate);
            if (!clashesBefore && !clashesAfter)
            {
                accepted.Insert(index, candidate);
            }
        }

        return accepted;
    }

    private static int InsertionIndex(List<Detection> sorted, int start)
    {
        int low = 0;
        int high = sorted.Count;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (sorted[mid].Start < start)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }
}
