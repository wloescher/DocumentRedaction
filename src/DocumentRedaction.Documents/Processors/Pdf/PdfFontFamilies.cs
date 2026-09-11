namespace DocumentRedaction.Documents.Processors.Pdf;

/// <summary>
/// Maps an embedded font's name to a generic family the renderer can honour. The original font
/// is not available, so serif, monospace and sans-serif are the fidelity on offer. Names are
/// used rather than the font descriptor's Serif and FixedPitch flags because PdfPig does not
/// expose the descriptor and real files set those flags unreliably.
/// </summary>
internal static class PdfFontFamilies
{
    public const string Serif = "serif";
    public const string Monospace = "monospace";
    public const string SansSerif = "sans-serif";

    private static readonly string[] SerifHints = ["Times", "Serif", "Georgia", "Garamond", "Book", "Cambria", "Palatino", "Minion", "Roman", "Baskerville", "Caslon", "Century"];
    private static readonly string[] MonospaceHints = ["Courier", "Mono", "Consolas", "Menlo", "Code"];

    public static string Classify(string? fontName)
    {
        if (string.IsNullOrEmpty(fontName))
        {
            return SansSerif;
        }

        // Subset prefixes ("ABCDEF+") and style suffixes are irrelevant to the family.
        string name = fontName[(fontName.IndexOf('+', StringComparison.Ordinal) + 1)..];
        if (MonospaceHints.Any(hint => Contains(name, hint)))
        {
            return Monospace;
        }

        // "Sans" outranks the serif hints so that "NotoSansSerif"-style names stay sans.
        if (Contains(name, "Sans"))
        {
            return SansSerif;
        }

        return SerifHints.Any(hint => Contains(name, hint)) ? Serif : SansSerif;
    }

    private static bool Contains(string name, string hint) => name.Contains(hint, StringComparison.OrdinalIgnoreCase);
}
