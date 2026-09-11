namespace DocumentRedaction.Documents;

/// <summary>
/// Caps that keep a small upload from expanding into unbounded memory or CPU while it is parsed.
/// Bound from configuration by the host; processors validate the instance they receive.
/// </summary>
public sealed class DocumentLimits
{
    public const long DefaultMaxDecodedBytes = 64L * 1024 * 1024;
    public const long DefaultMaxTotalDecodedBytes = 1024L * 1024 * 1024;
    public const long DefaultMaxTextCharacters = 5_000_000;
    public const int DefaultMaxPdfPages = 2_000;

    /// <summary>
    /// Largest decoded size of one container member: a PDF stream (content, object, font,
    /// image) or a Word package part. Enforced before the member is decoded wherever the format
    /// allows, so the cap bounds memory rather than just the end result.
    /// </summary>
    public long MaxDecodedBytes { get; set; } = DefaultMaxDecodedBytes;

    /// <summary>
    /// Budget for all stream decoding across one PDF, including the trial inflate used to
    /// measure large Flate streams. Bounds CPU when every stream is individually under the cap.
    /// </summary>
    public long MaxTotalDecodedBytes { get; set; } = DefaultMaxTotalDecodedBytes;

    /// <summary>
    /// Cap on the text extracted from a PDF across all pages. Every glyph is held with its
    /// geometry until the document is redrawn, roughly 120 bytes per character, so this cap is
    /// also the bound on memory per request.
    /// </summary>
    public long MaxTextCharacters { get; set; } = DefaultMaxTextCharacters;

    /// <summary>Most pages a PDF may have before it is rejected without extracting any text.</summary>
    public int MaxPdfPages { get; set; } = DefaultMaxPdfPages;

    /// <summary>Returns <paramref name="limits"/> (or the defaults when null) after validating them.</summary>
    public static DocumentLimits Validated(DocumentLimits? limits)
    {
        DocumentLimits effective = limits ?? new DocumentLimits();
        effective.Validate();
        return effective;
    }

    /// <summary>Throws <see cref="InvalidOperationException"/> naming the first property that is below 1.</summary>
    public void Validate()
    {
        if (!TryValidate(out string? error))
        {
            throw new InvalidOperationException(error);
        }
    }

    /// <summary>Reports the first property that is below 1, for hosts that collect validation errors rather than throw.</summary>
    public bool TryValidate(out string? error)
    {
        error = AtLeastOne(MaxDecodedBytes, nameof(MaxDecodedBytes))
            ?? AtLeastOne(MaxTotalDecodedBytes, nameof(MaxTotalDecodedBytes))
            ?? AtLeastOne(MaxTextCharacters, nameof(MaxTextCharacters))
            ?? AtLeastOne(MaxPdfPages, nameof(MaxPdfPages));
        return error is null;
    }

    /// <summary>The shared "must be at least 1" rule and message; null when <paramref name="value"/> passes.</summary>
    public static string? AtLeastOne(long value, string name) =>
        value < 1 ? $"{name} must be at least 1 but is {value}." : null;
}
