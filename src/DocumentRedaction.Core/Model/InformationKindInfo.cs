namespace DocumentRedaction.Core.Model;

/// <summary>Static metadata describing an <see cref="InformationKind"/>.</summary>
/// <param name="Kind">The kind being described.</param>
/// <param name="DisplayName">Human-readable name for UIs and reports.</param>
/// <param name="PlaceholderLabel">Label substituted into <see cref="RedactionOptions.PlaceholderFormat"/>.</param>
/// <param name="Categories">Every category that includes this kind.</param>
/// <param name="Priority">
/// Tie-breaker when two detections of equal length overlap. Lower values win; kinds whose
/// detectors validate a checksum are ranked ahead of purely pattern-based kinds.
/// </param>
public sealed record InformationKindInfo(
    InformationKind Kind,
    string DisplayName,
    string PlaceholderLabel,
    RedactionCategory Categories,
    int Priority);
