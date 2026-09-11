using DocumentFormat.OpenXml.Packaging;
using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;
using DocumentRedaction.Documents.Processors;
using DocumentRedaction.Tests.Fixtures;

namespace DocumentRedaction.Documents.Tests.Processors;

public class WordMetadataTests
{
    private readonly WordDocumentProcessor _processor = new(TextRedactor.CreateDefault());
    private readonly RedactionOptions _all = new();

    private static readonly WordFixture.CoreProperties Core = new(
        Creator: "Jane Doe",
        LastModifiedBy: "John Roe",
        Title: "Report for jane@example.com",
        Subject: "Card 4111 1111 1111 1111 on file",
        Keywords: "confidential; budget",
        Description: "Call 555-123-4567",
        Category: "Internal",
        ContentStatus: "Draft");

    [Fact]
    public void Core_author_fields_are_replaced_and_free_text_fields_redacted()
    {
        byte[] input = WordFixture.BuildDocument(document => WordFixture.SetCoreProperties(document, Core));

        ProcessedDocument processed = _processor.Redact(input, _all);
        WordFixture.CoreProperties after = WordFixture.ReadCoreProperties(processed.Content);

        Assert.Equal("[REDACTED-AUTHOR]", after.Creator);
        Assert.Equal("[REDACTED-AUTHOR]", after.LastModifiedBy);
        Assert.Equal("Report for [REDACTED-EMAIL]", after.Title);
        Assert.Equal("Card [REDACTED-CREDIT-CARD] on file", after.Subject);
        Assert.Equal("[REDACTED-CONFIDENTIAL]", after.Keywords);
        Assert.Equal("Call [REDACTED-PHONE]", after.Description);
        Assert.Equal("Internal", after.Category);
        Assert.Equal("Draft", after.ContentStatus);
        Assert.Equal(2, processed.Report.CountsByKind[InformationKind.DocumentAuthor]);
        Assert.Equal(6, processed.Report.Total);
    }

    [Fact]
    public void Empty_and_missing_properties_are_left_alone()
    {
        byte[] input = WordFixture.BuildDocument(document => WordFixture.SetCoreProperties(document, new WordFixture.CoreProperties(null, "", null, null, null, null, null, null)));

        ProcessedDocument processed = _processor.Redact(input, _all);

        WordFixture.CoreProperties after = WordFixture.ReadCoreProperties(processed.Content);
        Assert.Null(after.Creator);
        Assert.True(string.IsNullOrEmpty(after.LastModifiedBy));
        Assert.Equal(0, processed.Report.Total);
        Assert.Empty(processed.Warnings);
    }

    [Fact]
    public void Excluding_the_author_kind_keeps_author_fields_but_still_redacts_text_fields()
    {
        byte[] input = WordFixture.BuildDocument(document => WordFixture.SetCoreProperties(document, Core));
        RedactionOptions options = new() { ExcludedKinds = new HashSet<InformationKind> { InformationKind.DocumentAuthor } };

        WordFixture.CoreProperties after = WordFixture.ReadCoreProperties(_processor.Redact(input, options).Content);

        Assert.Equal("Jane Doe", after.Creator);
        Assert.Equal("John Roe", after.LastModifiedBy);
        Assert.Equal("Report for [REDACTED-EMAIL]", after.Title);
    }

    [Fact]
    public void Financial_only_selection_leaves_authors_and_redacts_the_card_in_the_subject()
    {
        byte[] input = WordFixture.BuildDocument(document => WordFixture.SetCoreProperties(document, Core));

        WordFixture.CoreProperties after = WordFixture.ReadCoreProperties(_processor.Redact(input, new RedactionOptions { Categories = RedactionCategory.Financial }).Content);

        Assert.Equal("Jane Doe", after.Creator);
        Assert.Equal("Card [REDACTED-CREDIT-CARD] on file", after.Subject);
        Assert.Equal("Report for jane@example.com", after.Title);
    }

    [Fact]
    public void Extended_properties_manager_is_replaced_and_company_redacted()
    {
        byte[] input = WordFixture.BuildDocument(document => WordFixture.SetExtendedProperties(document, "Acme (contact ops@acme.example)", "Jane Doe"));

        ProcessedDocument processed = _processor.Redact(input, _all);
        (string? company, string? manager) = WordFixture.ReadExtendedProperties(processed.Content);

        Assert.Equal("Acme (contact [REDACTED-EMAIL])", company);
        Assert.Equal("[REDACTED-AUTHOR]", manager);
        Assert.Equal(1, processed.Report.CountsByKind[InformationKind.DocumentAuthor]);
    }

    [Fact]
    public void Custom_string_properties_of_both_string_types_are_redacted_and_other_types_untouched()
    {
        // The fixture alternates the lpwstr and bstr string types across the values it is given.
        byte[] input = WordFixture.BuildDocument(document => WordFixture.SetCustomProperties(document, ("Client", "SSN 123-45-6789"), ("Contact", "mail a@b.co"), ("Note", "nothing here")));

        ProcessedDocument processed = _processor.Redact(input, _all);
        IReadOnlyDictionary<string, string> after = WordFixture.ReadCustomProperties(processed.Content);

        Assert.Equal("SSN [REDACTED-SSN]", after["Client"]);
        Assert.Equal("mail [REDACTED-EMAIL]", after["Contact"]);
        Assert.Equal("nothing here", after["Note"]);
        Assert.Equal("42", after["Revision"]);
        Assert.Equal(1, processed.Report.CountsByKind[InformationKind.SocialSecurityNumber]);
        Assert.Equal(1, processed.Report.CountsByKind[InformationKind.EmailAddress]);
    }

    [Fact]
    public void Empty_author_attribute_is_not_counted()
    {
        byte[] input = WordFixture.Build(main => main.Document!.Body!.Append(WordFixture.WithInsertedText("kept", "added", "")));

        ProcessedDocument processed = _processor.Redact(input, _all);

        Assert.False(processed.Report.CountsByKind.ContainsKey(InformationKind.DocumentAuthor));
        Assert.Equal([""], WordFixture.ReadAuthors(processed.Content).Authors);
    }

    [Fact]
    public void Authors_in_footnotes_are_scrubbed()
    {
        byte[] input = WordFixture.Build(main =>
        {
            main.Document!.Body!.Append(WordFixture.Paragraph("body"));
            WordFixture.AddFootnoteWithChange(main, "note", "Jane Doe");
        });

        Assert.Equal(["[REDACTED-AUTHOR]"], WordFixture.ReadAuthors(_processor.Redact(input, _all).Content).Authors);
    }

    [Fact]
    public void Comment_author_and_initials_are_scrubbed()
    {
        byte[] input = WordFixture.Build(main =>
        {
            main.Document!.Body!.Append(WordFixture.Paragraph("body"));
            WordFixture.AddCommentBy(main, "looks fine", "Jane Doe", "JD");
        });

        ProcessedDocument processed = _processor.Redact(input, _all);
        (IReadOnlyList<string> authors, IReadOnlyList<string> initials) = WordFixture.ReadAuthors(processed.Content);

        Assert.Equal(["[REDACTED-AUTHOR]"], authors);
        Assert.Empty(initials);
        Assert.Contains("looks fine", WordFixture.ReadAllText(processed.Content), StringComparison.Ordinal);
        Assert.Equal(1, processed.Report.CountsByKind[InformationKind.DocumentAuthor]);
    }

    [Fact]
    public void Tracked_change_authors_are_scrubbed_on_insertions_deletions_and_formatting_changes()
    {
        byte[] input = WordFixture.Build(main => main.Document!.Body!.Append(
            WordFixture.WithInsertedText("kept", "added", "Jane Doe"),
            WordFixture.WithDeletedText("kept", "gone"),
            WordFixture.WithFormattingChange("bolded", "John Roe")));

        ProcessedDocument processed = _processor.Redact(input, _all);
        (IReadOnlyList<string> authors, _) = WordFixture.ReadAuthors(processed.Content);

        Assert.Equal(3, authors.Count);
        Assert.All(authors, author => Assert.Equal("[REDACTED-AUTHOR]", author));
        Assert.Equal(3, processed.Report.CountsByKind[InformationKind.DocumentAuthor]);
        Assert.Contains("added", WordFixture.ReadAllText(processed.Content), StringComparison.Ordinal);
    }

    [Fact]
    public void Authors_in_headers_are_scrubbed_too()
    {
        byte[] input = WordFixture.Build(main =>
        {
            HeaderPartWithChange(main);
            main.Document!.Body!.Append(WordFixture.Paragraph("body"));
        });

        (IReadOnlyList<string> authors, _) = WordFixture.ReadAuthors(_processor.Redact(input, _all).Content);
        Assert.Equal(["[REDACTED-AUTHOR]"], authors);
    }

    private static void HeaderPartWithChange(MainDocumentPart main)
    {
        WordFixture.AddHeader(main, "header");
        HeaderPart header = main.HeaderParts.First();
        header.Header!.Append(WordFixture.WithInsertedText("h", "added", "Jane Doe"));
    }

    [Fact]
    public void Excluding_the_author_kind_keeps_comment_and_change_authors()
    {
        byte[] input = WordFixture.Build(main =>
        {
            main.Document!.Body!.Append(WordFixture.WithInsertedText("kept", "added", "Jane Doe"));
            WordFixture.AddCommentBy(main, "note", "John Roe", "JR");
        });
        RedactionOptions options = new() { ExcludedKinds = new HashSet<InformationKind> { InformationKind.DocumentAuthor } };

        (IReadOnlyList<string> authors, IReadOnlyList<string> initials) = WordFixture.ReadAuthors(_processor.Redact(input, options).Content);

        Assert.Equal(["Jane Doe", "John Roe"], authors.Order());
        Assert.Equal(["JR"], initials);
    }

    [Fact]
    public void People_part_is_removed_when_authors_are_redacted_and_kept_otherwise()
    {
        byte[] input = WordFixture.Build(main =>
        {
            main.Document!.Body!.Append(WordFixture.Paragraph("body"));
            WordFixture.AddPeoplePart(main, "Jane Doe");
        });
        Assert.True(WordFixture.HasPeoplePart(input));

        Assert.False(WordFixture.HasPeoplePart(_processor.Redact(input, _all).Content));
        RedactionOptions financial = new() { Categories = RedactionCategory.Financial };
        Assert.True(WordFixture.HasPeoplePart(_processor.Redact(input, financial).Content));
    }

    [Fact]
    public void Embedded_object_is_left_unchanged_and_reported_as_a_warning()
    {
        byte[] input = WordFixture.Build(main =>
        {
            main.Document!.Body!.Append(WordFixture.Paragraph("SSN 123-45-6789"));
            WordFixture.AddEmbeddedObject(main);
        });

        ProcessedDocument processed = _processor.Redact(input, _all);

        string warning = Assert.Single(processed.Warnings);
        Assert.StartsWith("1 embedded object was left unchanged", warning, StringComparison.Ordinal);
        Assert.Equal([1, 2, 3, 4], WordFixture.ReadEmbeddedObject(processed.Content));
        Assert.Contains("[REDACTED-SSN]", WordFixture.ReadAllText(processed.Content), StringComparison.Ordinal);
    }

    [Fact]
    public void Tracked_change_authors_in_the_styles_part_are_scrubbed()
    {
        byte[] input = WordFixture.Build(main =>
        {
            main.Document!.Body!.Append(WordFixture.Paragraph("body"));
            WordFixture.AddStyleWithChange(main, "Jane Doe");
        });
        Assert.Equal(["Jane Doe"], WordFixture.ReadStyleAuthors(input));

        ProcessedDocument processed = _processor.Redact(input, _all);

        Assert.Equal(["[REDACTED-AUTHOR]"], WordFixture.ReadStyleAuthors(processed.Content));
        Assert.Equal(1, processed.Report.CountsByKind[InformationKind.DocumentAuthor]);
    }

    [Fact]
    public void Imported_html_chunk_is_reported_as_unredacted()
    {
        byte[] input = WordFixture.Build(main =>
        {
            main.Document!.Body!.Append(WordFixture.Paragraph("body"));
            WordFixture.AddHtmlChunk(main, "<html><body>SSN 123-45-6789</body></html>");
        });

        ProcessedDocument processed = _processor.Redact(input, _all);

        string warning = Assert.Single(processed.Warnings);
        Assert.StartsWith("1 imported-content or SmartArt part was left unchanged", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Embedded_packages_and_objects_in_headers_are_counted_too()
    {
        byte[] input = WordFixture.Build(main =>
        {
            main.Document!.Body!.Append(WordFixture.Paragraph("body"));
            WordFixture.AddEmbeddedObject(main);
            WordFixture.AddEmbeddedPackage(main);
            WordFixture.AddEmbeddedObjectInHeader(main);
        });

        string warning = Assert.Single(_processor.Redact(input, _all).Warnings);
        Assert.StartsWith("3 embedded objects were left unchanged", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancellation_is_honoured_during_the_metadata_pass()
    {
        byte[] input = WordFixture.BuildDocument(document => WordFixture.SetCoreProperties(document, Core));
        using CancellationTokenSource cts = new();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => _processor.Redact(input, _all, cts.Token));
    }

    [Fact]
    public void Placeholder_format_applies_to_authors() =>
        Assert.Equal("<AUTHOR>", WordFixture.ReadCoreProperties(_processor.Redact(
            WordFixture.BuildDocument(document => WordFixture.SetCoreProperties(document, Core)),
            new RedactionOptions { PlaceholderFormat = "<{0}>" }).Content).Creator);
}
