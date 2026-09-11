namespace DocumentRedaction.Documents.Processors.Pdf;

/// <summary>
/// How redaction labels are drawn, shared by the redactor that sizes them and the renderer that
/// draws them so the fit estimate and the font can never drift apart.
/// </summary>
internal static class PdfLabelStyle
{
    public const string FontFamily = PdfFontFamilies.SansSerif;

    /// <summary>Average advance of an upper-case sans-serif glyph, as a fraction of the point size.</summary>
    public const double WidthPerPoint = 0.72;

    /// <summary>Below this a label is unreadable, so the box is drawn without one.</summary>
    public const double MinPointSize = 4;
}
