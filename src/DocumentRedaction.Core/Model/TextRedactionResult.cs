namespace DocumentRedaction.Core.Model;

/// <summary>Outcome of redacting a single block of text.</summary>
/// <param name="RedactedText">The text with placeholders substituted.</param>
/// <param name="Detections">Non-overlapping detections applied, ordered by position in the original text.</param>
/// <param name="Report">Counts by kind.</param>
public sealed record TextRedactionResult(
    string RedactedText,
    IReadOnlyList<Detection> Detections,
    RedactionReport Report);
