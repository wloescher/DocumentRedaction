using System.IO.Compression;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;

namespace DocumentRedaction.Tests.Fixtures;

/// <summary>Builds small PDFs in memory with QuestPDF and reads them back with PdfPig.</summary>
public static class PdfFixture
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
    public static byte[] BlankPage() => RawPdf(PageObjects(pageCount: 1, content: null));

    /// <summary>
    /// A PDF of <paramref name="pages"/> pages whose Flate-compressed content streams each inflate
    /// to <paramref name="decodedBytes"/> of whitespace: valid, text-free, and a few kilobytes on disk.
    /// </summary>
    public static byte[] FlateBomb(int decodedBytes, int pages = 1) =>
        RawPdf(PageObjects(pages, Deflate(decodedBytes, zlib: true)));

    /// <summary>
    /// A zero-page PDF whose only cross-reference is an xref stream that inflates to
    /// <paramref name="decodedBytes"/>, followed by a stray classic trailer so that PdfPig's
    /// brute-force recovery still opens the file after it swallows the xref-stream failure.
    /// </summary>
    public static byte[] XrefStreamBomb(int decodedBytes)
    {
        byte[] compressed = Deflate(decodedBytes, zlib: true);
        byte[][] objects =
        [
            Ascii("<< /Type /Catalog /Pages 2 0 R >>"),
            Ascii("<< /Type /Pages /Kids [] /Count 0 >>"),
            [.. Ascii($"<< /Type /XRef /Size 4 /W [1 2 1] /Root 1 0 R /Filter /FlateDecode /Length {compressed.Length} >>\nstream\n"), .. compressed, .. Ascii("\nendstream")],
        ];

        using MemoryStream stream = new();
        List<long> offsets = WriteObjects(stream, objects);
        stream.Write(Ascii($"trailer << /Root 1 0 R /Size 4 >>\nstartxref\n{offsets[2]}\n%%EOF\n"));
        return stream.ToArray();
    }

    /// <summary>Compresses <paramref name="decodedBytes"/> of spaces, framed as zlib (PDF's FlateDecode) or raw deflate.</summary>
    public static byte[] Deflate(int decodedBytes, bool zlib)
    {
        using MemoryStream buffer = new();
        using (Stream deflater = zlib
            ? new ZLibStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true)
            : new DeflateStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            byte[] chunk = new byte[64 * 1024];
            Array.Fill(chunk, (byte)' ');
            int remaining = decodedBytes;
            while (remaining > 0)
            {
                int count = Math.Min(remaining, chunk.Length);
                deflater.Write(chunk, 0, count);
                remaining -= count;
            }
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Catalog (object 1), page tree (object 2) and <paramref name="pageCount"/> Letter pages. With
    /// <paramref name="content"/>, every page gets its own Flate content stream object holding those bytes.
    /// </summary>
    private static byte[][] PageObjects(int pageCount, byte[]? content)
    {
        int stride = content is null ? 1 : 2;
        string kids = string.Join(' ', Enumerable.Range(0, pageCount).Select(i => $"{3 + i * stride} 0 R"));
        List<byte[]> objects =
        [
            Ascii("<< /Type /Catalog /Pages 2 0 R >>"),
            Ascii($"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>"),
        ];

        for (int i = 0; i < pageCount; i++)
        {
            int pageNumber = 3 + i * stride;
            string contents = content is null ? "" : $" /Contents {pageNumber + 1} 0 R";
            objects.Add(Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792]{contents} >>"));
            if (content is not null)
            {
                objects.Add([.. Ascii($"<< /Length {content.Length} /Filter /FlateDecode >>\nstream\n"), .. content, .. Ascii("\nendstream")]);
            }
        }

        return [.. objects];
    }

    /// <summary>Writes objects 1..n (bodies only) with a correct cross-reference table; object 1 must be the catalog.</summary>
    private static byte[] RawPdf(byte[][] objects)
    {
        using MemoryStream stream = new();
        List<long> offsets = WriteObjects(stream, objects);

        long xref = stream.Position;
        StringBuilder tail = new();
        tail.Append("xref\n0 ").Append(objects.Length + 1).Append("\n0000000000 65535 f \n");
        foreach (long offset in offsets)
        {
            tail.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        tail.Append("trailer << /Size ").Append(objects.Length + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        stream.Write(Ascii(tail.ToString()));
        return stream.ToArray();
    }

    /// <summary>Writes the header and each object as "N 0 obj ... endobj", returning every object's byte offset.</summary>
    private static List<long> WriteObjects(MemoryStream stream, byte[][] objects)
    {
        stream.Write(Ascii("%PDF-1.5\n"));
        List<long> offsets = [];
        for (int i = 0; i < objects.Length; i++)
        {
            offsets.Add(stream.Position);
            stream.Write(Ascii($"{i + 1} 0 obj\n"));
            stream.Write(objects[i]);
            stream.Write(Ascii("\nendobj\n"));
        }

        return offsets;
    }

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

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
