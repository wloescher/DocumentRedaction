using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;

namespace DocumentRedaction.Documents.Tests.Fixtures;

/// <summary>Builds small PDFs in memory with QuestPDF and reads them back with PdfPig.</summary>
internal static class PdfFixture
{
    static PdfFixture()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>One page per entry; each entry's paragraphs become separate text blocks.</summary>
    public static byte[] Build(params string[][] pages) => Build(PageSizes.Letter, pages);

    public static byte[] Build(PageSize size, params string[][] pages) =>
        Document.Create(container =>
        {
            foreach (string[] paragraphs in pages)
            {
                container.Page(page =>
                {
                    page.Size(size);
                    page.Margin(40);
                    page.Content().Column(column =>
                    {
                        column.Spacing(12);
                        foreach (string paragraph in paragraphs)
                        {
                            column.Item().Text(paragraph);
                        }
                    });
                });
            }
        }).GeneratePdf();

    /// <summary>A syntactically valid single-page PDF with no content stream at all.</summary>
    public static byte[] BlankPage() => Encoding.ASCII.GetBytes(
        "%PDF-1.4\n" +
        "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n" +
        "2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n" +
        "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >> endobj\n" +
        "xref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n0000000115 00000 n \n" +
        "trailer << /Size 4 /Root 1 0 R >>\nstartxref\n190\n%%EOF\n");

    public static IReadOnlyList<string> ReadPageTexts(ReadOnlyMemory<byte> content)
    {
        using PdfDocument document = PdfDocument.Open(content.ToArray());
        return document.GetPages().Select(page => page.Text).ToList();
    }

    public static IReadOnlyList<(double Width, double Height)> ReadPageSizes(ReadOnlyMemory<byte> content)
    {
        using PdfDocument document = PdfDocument.Open(content.ToArray());
        return document.GetPages().Select(page => (page.Width, page.Height)).ToList();
    }
}
