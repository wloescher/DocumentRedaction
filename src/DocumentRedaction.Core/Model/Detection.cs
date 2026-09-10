namespace DocumentRedaction.Core.Model;

/// <summary>A span of text identified as sensitive.</summary>
/// <param name="Kind">What was found.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters in the span.</param>
/// <param name="Text">The matched text, retained for testing and diagnostics. Never log it.</param>
public sealed record Detection(InformationKind Kind, int Start, int Length, string Text)
{
    /// <summary>Exclusive end offset.</summary>
    public int End => Start + Length;

    public bool Overlaps(Detection other) => Start < other.End && other.Start < End;
}
