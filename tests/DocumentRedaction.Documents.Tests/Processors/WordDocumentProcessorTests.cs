using DocumentFormat.OpenXml.Wordprocessing;
using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;
using DocumentRedaction.Documents.Processors;
using DocumentRedaction.Documents.Tests.Fixtures;

namespace DocumentRedaction.Documents.Tests.Processors;

public class WordDocumentProcessorTests
{
    private readonly WordDocumentProcessor _processor = new(TextRedactor.CreateDefault());
    private readonly RedactionOptions _options = new();

    [Fact]
    public void Redacts_body_paragraphs_and_reports()
    {
        byte[] input = WordFixture.WithParagraphs("Hello", "SSN 123-45-6789", "mail a@b.co");
        ProcessedDocument processed = _processor.Redact(input, _options);

        Assert.Equal(["Hello", "SSN [REDACTED-SSN]", "mail [REDACTED-EMAIL]"], WordFixture.ReadParagraphs(processed.Content));
        Assert.Equal(2, processed.Report.Total);
    }

    [Fact]
    public void Identifier_split_across_runs_is_found_and_formatting_kept()
    {
        byte[] input = WordFixture.Build(main => main.Document!.Body!.Append(
            new Paragraph(WordFixture.Run("SSN 123-"), WordFixture.Run("45-", bold: true), WordFixture.Run("6789 end"))));

        ProcessedDocument processed = _processor.Redact(input, _options);
        IReadOnlyList<Run> runs = WordFixture.ReadBodyRuns(processed.Content, 0);

        Assert.Equal(3, runs.Count);
        Assert.Equal(["SSN [REDACTED-SSN]", "", " end"], runs.Select(run => run.InnerText));
        Assert.NotNull(runs[1].RunProperties?.Bold);
        Assert.Equal(1, processed.Report.Total);
    }

    [Fact]
    public void Multiple_detections_in_one_run_and_across_runs()
    {
        byte[] input = WordFixture.Build(main => main.Document!.Body!.Append(
            new Paragraph(WordFixture.Run("a@b.co and 123-45-6789 then 4111 1111 "), WordFixture.Run("1111 1111."))));

        ProcessedDocument processed = _processor.Redact(input, _options);
        IReadOnlyList<Run> runs = WordFixture.ReadBodyRuns(processed.Content, 0);

        Assert.Equal(["[REDACTED-EMAIL] and [REDACTED-SSN] then [REDACTED-CREDIT-CARD]", "."], runs.Select(run => run.InnerText));
        Assert.Equal(3, processed.Report.Total);
    }

    [Fact]
    public void Tab_between_runs_separates_words()
    {
        byte[] input = WordFixture.Build(main => main.Document!.Body!.Append(
            new Paragraph(WordFixture.Run("John"), new Run(new TabChar()), WordFixture.Run("Smith"))));
        RedactionOptions options = new() { Categories = RedactionCategory.None, CustomTerms = ["JohnSmith", "John Smith"] };

        ProcessedDocument processed = _processor.Redact(input, options);

        // "John Smith" matches across the tab (custom terms allow flexible whitespace); "JohnSmith" must not.
        // The placeholder lands in the first run, the tab element survives, and the last run is emptied.
        IReadOnlyList<Run> runs = WordFixture.ReadBodyRuns(processed.Content, 0);
        Assert.Equal(["[REDACTED-CUSTOM]", "", ""], runs.Select(run => run.InnerText));
        Assert.Single(runs[1].Elements<TabChar>());
        Assert.Equal(1, processed.Report.Total);
    }

    [Fact]
    public void Table_cells_are_redacted()
    {
        byte[] input = WordFixture.Build(main => main.Document!.Body!.Append(WordFixture.Table("Name", "SSN 123-45-6789")));
        ProcessedDocument processed = _processor.Redact(input, _options);
        Assert.Contains("SSN [REDACTED-SSN]", WordFixture.ReadParagraphs(processed.Content));
    }

    [Fact]
    public void Header_footer_footnote_and_comment_are_redacted()
    {
        byte[] input = WordFixture.Build(main =>
        {
            main.Document!.Body!.Append(WordFixture.Paragraph("body"));
            WordFixture.AddHeader(main, "header a@b.co");
            WordFixture.AddFooter(main, "footer 123-45-6789");
            WordFixture.AddFootnote(main, "note (555) 123-4567");
            WordFixture.AddComment(main, "comment 4111111111111111");
        });

        ProcessedDocument processed = _processor.Redact(input, _options);
        string text = WordFixture.ReadAllText(processed.Content);

        Assert.Contains("header [REDACTED-EMAIL]", text, StringComparison.Ordinal);
        Assert.Contains("footer [REDACTED-SSN]", text, StringComparison.Ordinal);
        Assert.Contains("note [REDACTED-PHONE]", text, StringComparison.Ordinal);
        Assert.Contains("comment [REDACTED-CREDIT-CARD]", text, StringComparison.Ordinal);
        Assert.Equal(4, processed.Report.Total);
    }

    [Fact]
    public void Tracked_deleted_text_is_redacted()
    {
        byte[] input = WordFixture.Build(main => main.Document!.Body!.Append(WordFixture.WithDeletedText("kept ", "deleted 123-45-6789")));
        ProcessedDocument processed = _processor.Redact(input, _options);
        Assert.Equal("kept deleted [REDACTED-SSN]", WordFixture.ReadParagraphs(processed.Content)[0]);
    }

    [Fact]
    public void Text_box_paragraph_is_redacted_exactly_once()
    {
        byte[] input = WordFixture.Build(main => main.Document!.Body!.Append(WordFixture.WithTextBox("outer a@b.co ", "inner 123-45-6789")));
        ProcessedDocument processed = _processor.Redact(input, _options);

        IReadOnlyList<string> paragraphs = WordFixture.ReadParagraphs(processed.Content);
        Assert.Contains("inner [REDACTED-SSN]", paragraphs);
        Assert.Contains(paragraphs, p => p.StartsWith("outer [REDACTED-EMAIL] ", StringComparison.Ordinal));
        Assert.Equal(2, processed.Report.Total);
    }

    [Fact]
    public void Hyperlink_relationship_target_with_sensitive_value_is_blanked()
    {
        byte[] input = WordFixture.Build(main =>
        {
            WordFixture.AddHyperlink(main, "mailto:john@example.com", "mail John");
            WordFixture.AddHyperlink(main, "https://example.com/help", "help");
        });

        ProcessedDocument processed = _processor.Redact(input, _options);

        Assert.Equal(["about:blank", "https://example.com/help"], WordFixture.ReadHyperlinkTargets(processed.Content).Order(StringComparer.Ordinal));
        Assert.Contains("mail John", WordFixture.ReadParagraphs(processed.Content));
        Assert.Equal(1, processed.Report.Total);
    }

    [Fact]
    public void Hyperlink_field_code_is_redacted()
    {
        byte[] input = WordFixture.Build(main => main.Document!.Body!.Append(WordFixture.HyperlinkField("mailto:john@example.com", "mail John")));
        ProcessedDocument processed = _processor.Redact(input, _options);

        string code = Assert.Single(WordFixture.ReadFieldCodes(processed.Content));
        Assert.Equal(" HYPERLINK \"mailto:[REDACTED-EMAIL]\" ", code);
        Assert.Equal(1, processed.Report.Total);
    }

    [Fact]
    public void Document_without_detections_is_unchanged()
    {
        byte[] input = WordFixture.WithParagraphs("nothing", "to see");
        ProcessedDocument processed = _processor.Redact(input, _options);
        Assert.Equal(["nothing", "to see"], WordFixture.ReadParagraphs(processed.Content));
        Assert.Equal(0, processed.Report.Total);
    }

    [Fact]
    public void Empty_document_is_accepted()
    {
        ProcessedDocument processed = _processor.Redact(WordFixture.WithParagraphs(), _options);
        Assert.Empty(WordFixture.ReadParagraphs(processed.Content));
    }

    [Fact]
    public void Original_values_never_survive_in_the_package()
    {
        byte[] input = WordFixture.WithParagraphs("SSN 123-45-6789");
        ProcessedDocument processed = _processor.Redact(input, _options);
        Assert.DoesNotContain("123-45-6789", WordFixture.ReadAllText(processed.Content), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(new byte[] { 1, 2, 3 })]
    [InlineData(new byte[0])]
    public void Corrupt_input_throws_invalid_document(byte[] input) =>
        Assert.Throws<InvalidDocumentException>(() => _processor.Redact(input, _options));

    [Fact]
    public void Malformed_xml_part_throws_invalid_document()
    {
        // A valid package whose document.xml is not well-formed XML.
        byte[] input;
        using (MemoryStream stream = new())
        {
            using (System.IO.Compression.ZipArchive zip = new(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                using MemoryStream valid = new(WordFixture.WithParagraphs("x"));
                using System.IO.Compression.ZipArchive source = new(valid, System.IO.Compression.ZipArchiveMode.Read);
                foreach (System.IO.Compression.ZipArchiveEntry entry in source.Entries)
                {
                    using Stream target = zip.CreateEntry(entry.FullName).Open();
                    if (entry.FullName == "word/document.xml")
                    {
                        target.Write("<w:document><unclosed"u8);
                    }
                    else
                    {
                        using Stream original = entry.Open();
                        original.CopyTo(target);
                    }
                }
            }

            input = stream.ToArray();
        }

        Assert.Throws<InvalidDocumentException>(() => _processor.Redact(input, _options));
    }

    [Fact]
    public void Honours_cancellation()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => _processor.Redact(WordFixture.WithParagraphs("x"), _options, cts.Token));
    }
}
