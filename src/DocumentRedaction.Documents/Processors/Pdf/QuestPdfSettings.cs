using QuestPDF.Infrastructure;

namespace DocumentRedaction.Documents.Processors.Pdf;

/// <summary>
/// The one place that writes QuestPDF's process-wide settings. The library keeps its license
/// and rendering switches in static state, so whichever <see cref="PdfDocumentProcessor"/> was
/// constructed last decides the license; hosts register a single processor.
/// </summary>
internal static class QuestPdfSettings
{
    public static void Apply(QuestPdfLicense license)
    {
        QuestPDF.Settings.License = ToLicenseType(license);
        // Redacted documents may contain glyphs outside the bundled font; render what we can.
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    /// <summary>Maps the host-facing enum onto QuestPDF's so the library type stays inside Documents.</summary>
    public static LicenseType ToLicenseType(QuestPdfLicense license) => license switch
    {
        QuestPdfLicense.Community => LicenseType.Community,
        QuestPdfLicense.Professional => LicenseType.Professional,
        QuestPdfLicense.Enterprise => LicenseType.Enterprise,
        _ => throw new ArgumentOutOfRangeException(nameof(license), license, "Unknown QuestPDF license."),
    };
}
