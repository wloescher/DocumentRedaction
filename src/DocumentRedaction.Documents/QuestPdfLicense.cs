namespace DocumentRedaction.Documents;

/// <summary>
/// The QuestPDF license the host is entitled to. QuestPDF renders the redacted PDFs; its
/// Community license is free below the revenue threshold published at questpdf.com and the
/// host must declare a paid tier above it. The value is applied to the library's global
/// settings when the PDF processor is constructed.
/// </summary>
public enum QuestPdfLicense
{
    Community,
    Professional,
    Enterprise,
}
