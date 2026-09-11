using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.VariantTypes;
using DocumentFormat.OpenXml.Wordprocessing;
using Ext = DocumentFormat.OpenXml.ExtendedProperties;
using W15 = DocumentFormat.OpenXml.Office2013.Word;
using V = DocumentFormat.OpenXml.Vml;

namespace DocumentRedaction.Tests.Fixtures;

/// <summary>Builds small .docx files in memory so tests need no binary fixtures.</summary>
public static class WordFixture
{
    public static byte[] Build(Action<MainDocumentPart> configure) => BuildDocument(document => configure(document.MainDocumentPart!));

    /// <summary>Like <see cref="Build"/>, with the whole package available for properties and extra parts.</summary>
    public static byte[] BuildDocument(Action<WordprocessingDocument> configure)
    {
        using MemoryStream stream = new();
        using (WordprocessingDocument document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart main = document.AddMainDocumentPart();
            main.Document = new Document(new Body());
            configure(document);
            main.Document!.Save();
        }

        return stream.ToArray();
    }

    public sealed record CoreProperties(string? Creator, string? LastModifiedBy, string? Title, string? Subject, string? Keywords, string? Description, string? Category, string? ContentStatus);

    public static void SetCoreProperties(WordprocessingDocument document, CoreProperties values)
    {
        var properties = document.PackageProperties;
        properties.Creator = values.Creator;
        properties.LastModifiedBy = values.LastModifiedBy;
        properties.Title = values.Title;
        properties.Subject = values.Subject;
        properties.Keywords = values.Keywords;
        properties.Description = values.Description;
        properties.Category = values.Category;
        properties.ContentStatus = values.ContentStatus;
    }

    public static CoreProperties ReadCoreProperties(ReadOnlyMemory<byte> content) => Read(content, document =>
    {
        var p = document.PackageProperties;
        return new CoreProperties(p.Creator, p.LastModifiedBy, p.Title, p.Subject, p.Keywords, p.Description, p.Category, p.ContentStatus);
    });

    public static void SetExtendedProperties(WordprocessingDocument document, string company, string manager)
    {
        ExtendedFilePropertiesPart part = document.AddExtendedFilePropertiesPart();
        part.Properties = new Ext.Properties(new Ext.Company(company), new Ext.Manager(manager));
    }

    public static (string? Company, string? Manager) ReadExtendedProperties(ReadOnlyMemory<byte> content) => Read(content, document =>
    {
        Ext.Properties? properties = document.ExtendedFilePropertiesPart?.Properties;
        return (properties?.Company?.Text, properties?.Manager?.Text);
    });

    /// <summary>
    /// Adds custom properties: string-valued ones (name, value; every other one as the older
    /// bstr type) plus one integer property that must be left alone.
    /// </summary>
    public static void SetCustomProperties(WordprocessingDocument document, params (string Name, string Value)[] values)
    {
        const string formatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}";
        CustomFilePropertiesPart part = document.AddCustomFilePropertiesPart();
        part.Properties = new Properties();
        int id = 2;
        foreach ((string name, string value) in values)
        {
            OpenXmlElement typed = id % 2 == 0 ? new VTLPWSTR(value) : new VTBString(value);
            part.Properties.Append(new CustomDocumentProperty(typed) { FormatId = formatId, PropertyId = id++, Name = name });
        }

        part.Properties.Append(new CustomDocumentProperty(new VTInt32("42")) { FormatId = formatId, PropertyId = id, Name = "Revision" });
    }

    public static IReadOnlyDictionary<string, string> ReadCustomProperties(ReadOnlyMemory<byte> content) => Read(content, document =>
        document.CustomFilePropertiesPart!.Properties!.Elements<CustomDocumentProperty>()
            .ToDictionary(property => property.Name!.Value!, property => property.InnerText, StringComparer.Ordinal));

    public static Paragraph WithInsertedText(string visible, string inserted, string author) =>
        new(Run(visible), new InsertedRun(Run(inserted)) { Id = "2", Author = author, Date = DateTime.UtcNow });

    /// <summary>A run whose formatting change is tracked, which names its author on the change record.</summary>
    public static Paragraph WithFormattingChange(string text, string author)
    {
        Run run = Run(text, bold: true);
        run.RunProperties!.Append(new RunPropertiesChange(new PreviousRunProperties()) { Id = "3", Author = author, Date = DateTime.UtcNow });
        return new Paragraph(run);
    }

    public static void AddCommentBy(MainDocumentPart main, string text, string author, string initials)
    {
        WordprocessingCommentsPart part = main.AddNewPart<WordprocessingCommentsPart>();
        part.Comments = new Comments(new Comment(Paragraph(text)) { Id = "1", Author = author, Initials = initials });
    }

    public static void AddPeoplePart(MainDocumentPart main, string author)
    {
        WordprocessingPeoplePart part = main.AddNewPart<WordprocessingPeoplePart>();
        part.People = new W15.People(new W15.Person(new W15.PresenceInfo { ProviderId = "None", UserId = author }) { Author = author });
    }

    /// <summary>A style whose run properties carry a tracked change naming its author, in styles.xml.</summary>
    public static void AddStyleWithChange(MainDocumentPart main, string author)
    {
        StyleDefinitionsPart part = main.AddNewPart<StyleDefinitionsPart>();
        Style style = new(new StyleRunProperties(new RunPropertiesChange(new PreviousRunProperties()) { Id = "9", Author = author, Date = DateTime.UtcNow })) { Type = StyleValues.Paragraph, StyleId = "Changed" };
        part.Styles = new Styles(style);
    }

    /// <summary>All w:author values in the styles part, or empty when there is none.</summary>
    public static IReadOnlyList<string> ReadStyleAuthors(ReadOnlyMemory<byte> content) => Read(content, document =>
        document.MainDocumentPart!.StyleDefinitionsPart?.Styles?.Descendants<RunPropertiesChange>().Select(change => change.Author?.Value ?? string.Empty).ToList() ?? []);

    /// <summary>An imported HTML chunk (altChunk), whose text the redactor never reads.</summary>
    public static void AddHtmlChunk(MainDocumentPart main, string html)
    {
        AlternativeFormatImportPart part = main.AddAlternativeFormatImportPart(AlternativeFormatImportPartType.Html, "chunk1");
        using MemoryStream data = new(System.Text.Encoding.UTF8.GetBytes(html));
        part.FeedData(data);
        main.Document!.Body!.Append(new AltChunk { Id = "chunk1" });
    }

    public static void AddEmbeddedObjectInHeader(MainDocumentPart main)
    {
        AddHeader(main, "header");
        EmbeddedObjectPart part = main.HeaderParts.First().AddEmbeddedObjectPart("application/vnd.openxmlformats-officedocument.oleObject");
        using MemoryStream data = new([9, 9]);
        part.FeedData(data);
    }

    public static bool HasPeoplePart(ReadOnlyMemory<byte> content) => Read(content, document =>
        document.MainDocumentPart!.GetPartsOfType<WordprocessingPeoplePart>().Any());

    public static void AddEmbeddedObject(MainDocumentPart main)
    {
        EmbeddedObjectPart part = main.AddEmbeddedObjectPart("application/vnd.openxmlformats-officedocument.oleObject");
        using MemoryStream data = new([1, 2, 3, 4]);
        part.FeedData(data);
    }

    /// <summary>An embedded Office package (a spreadsheet pasted as an object) rather than a raw OLE stream.</summary>
    public static void AddEmbeddedPackage(MainDocumentPart main)
    {
        EmbeddedPackagePart part = main.AddEmbeddedPackagePart("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        using MemoryStream data = new([5, 6, 7]);
        part.FeedData(data);
    }

    public static byte[]? ReadEmbeddedObject(ReadOnlyMemory<byte> content) => Read(content, document =>
    {
        EmbeddedObjectPart? part = document.MainDocumentPart!.EmbeddedObjectParts.FirstOrDefault();
        if (part is null)
        {
            return null;
        }

        using MemoryStream data = new();
        part.GetStream().CopyTo(data);
        return data.ToArray();
    });

    public static void AddFootnoteWithChange(MainDocumentPart main, string text, string author)
    {
        FootnotesPart part = main.AddNewPart<FootnotesPart>();
        part.Footnotes = new Footnotes(new Footnote(WithInsertedText(text, "added", author)) { Id = 1 });
        main.Document!.Body!.Append(new Paragraph(new Run(new FootnoteReference { Id = 1 })));
    }

    /// <summary>Every w:author value across the text-bearing parts, and every w:initials value.</summary>
    public static (IReadOnlyList<string> Authors, IReadOnlyList<string> Initials) ReadAuthors(ReadOnlyMemory<byte> content) => Read(content, document =>
    {
        List<string> authors = [];
        List<string> initials = [];
        foreach (OpenXmlElement element in TextBearingParts(document.MainDocumentPart!).SelectMany(part => part.RootElement!.Descendants()))
        {
            foreach (OpenXmlAttribute attribute in element.GetAttributes())
            {
                if (attribute.LocalName == "author")
                {
                    authors.Add(attribute.Value ?? string.Empty);
                }
                else if (attribute.LocalName == "initials")
                {
                    initials.Add(attribute.Value ?? string.Empty);
                }
            }
        }

        return ((IReadOnlyList<string>)authors, (IReadOnlyList<string>)initials);
    });

    /// <summary>Opens the package read-only and hands it to <paramref name="read"/>; every reader goes through here.</summary>
    private static T Read<T>(ReadOnlyMemory<byte> content, Func<WordprocessingDocument, T> read)
    {
        using MemoryStream stream = new(content.ToArray());
        using WordprocessingDocument document = WordprocessingDocument.Open(stream, isEditable: false);
        return read(document);
    }

    /// <summary>The same parts the processor treats as text-bearing: body, headers, footers, footnotes, endnotes, comments.</summary>
    private static IEnumerable<OpenXmlPart> TextBearingParts(MainDocumentPart main)
    {
        IEnumerable<OpenXmlPart> parts = [main, .. main.HeaderParts, .. main.FooterParts];
        if (main.FootnotesPart is { } footnotes)
        {
            parts = parts.Append(footnotes);
        }

        if (main.EndnotesPart is { } endnotes)
        {
            parts = parts.Append(endnotes);
        }

        if (main.WordprocessingCommentsPart is { } comments)
        {
            parts = parts.Append(comments);
        }

        return parts;
    }

    public static byte[] WithParagraphs(params string[] paragraphs) =>
        Build(main => main.Document!.Body!.Append(paragraphs.Select(text => Paragraph(text)).ToArray()));

    public static Paragraph Paragraph(params string[] runTexts) =>
        new(runTexts.Select(text => Run(text)).ToArray());

    public static Run Run(string text, bool bold = false)
    {
        Run run = new();
        if (bold)
        {
            run.RunProperties = new RunProperties(new Bold());
        }

        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    public static Table Table(params string[] cellTexts) =>
        new(new TableRow(cellTexts.Select(text => new TableCell(Paragraph(text))).ToArray()));

    public static void AddHeader(MainDocumentPart main, string text)
    {
        HeaderPart part = main.AddNewPart<HeaderPart>();
        part.Header = new Header(Paragraph(text));
        AddSectionReference(main, new HeaderReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(part) });
    }

    public static void AddFooter(MainDocumentPart main, string text)
    {
        FooterPart part = main.AddNewPart<FooterPart>();
        part.Footer = new Footer(Paragraph(text));
        AddSectionReference(main, new FooterReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(part) });
    }

    public static void AddFootnote(MainDocumentPart main, string text)
    {
        FootnotesPart part = main.AddNewPart<FootnotesPart>();
        part.Footnotes = new Footnotes(new Footnote(Paragraph(text)) { Id = 1 });
        main.Document!.Body!.Append(new Paragraph(new Run(new FootnoteReference { Id = 1 })));
    }

    public static void AddComment(MainDocumentPart main, string text)
    {
        WordprocessingCommentsPart part = main.AddNewPart<WordprocessingCommentsPart>();
        part.Comments = new Comments(new Comment(Paragraph(text)) { Id = "1", Author = "tester" });
    }

    public static Paragraph WithDeletedText(string visible, string deleted) =>
        new(Run(visible), new DeletedRun(new Run(new DeletedText(deleted) { Space = SpaceProcessingModeValues.Preserve })) { Id = "1", Author = "tester" });

    /// <summary>A paragraph holding a VML text box whose content is itself a paragraph.</summary>
    public static Paragraph WithTextBox(string outerText, string innerText) =>
        new(Run(outerText), new Run(new Picture(new V.Shape(new V.TextBox(new TextBoxContent(Paragraph(innerText)))))));

    public static void AddHyperlink(MainDocumentPart main, string target, string displayText)
    {
        HyperlinkRelationship relationship = main.AddHyperlinkRelationship(new Uri(target), isExternal: true);
        main.Document!.Body!.Append(new Paragraph(new Hyperlink(Run(displayText)) { Id = relationship.Id }));
    }

    /// <summary>A legacy HYPERLINK field: begin, instruction text, separator, display text, end.</summary>
    public static Paragraph HyperlinkField(string target, string displayText) =>
        new(
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode($" HYPERLINK \"{target}\" ") { Space = SpaceProcessingModeValues.Preserve }),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            Run(displayText),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End }));

    public static IReadOnlyList<string> ReadHyperlinkTargets(ReadOnlyMemory<byte> content) => Read(content, document =>
        document.MainDocumentPart!.HyperlinkRelationships.Select(link => link.Uri.OriginalString).ToList());

    public static IReadOnlyList<string> ReadFieldCodes(ReadOnlyMemory<byte> content) => Read(content, document =>
        document.MainDocumentPart!.Document!.Body!.Descendants<FieldCode>().Select(code => code.Text).ToList());

    private static void AddSectionReference(MainDocumentPart main, OpenXmlElement reference)
    {
        Body body = main.Document!.Body!;
        SectionProperties section = body.Elements<SectionProperties>().FirstOrDefault() ?? body.AppendChild(new SectionProperties());
        section.PrependChild(reference);
    }

    /// <summary>All text in the document, one entry per paragraph, across every text-bearing part.</summary>
    public static IReadOnlyList<string> ReadParagraphs(ReadOnlyMemory<byte> content) => Read(content, document =>
        TextBearingParts(document.MainDocumentPart!).SelectMany(part => part.RootElement!.Descendants<Paragraph>()).Select(p => p.InnerText).ToList());

    public static string ReadAllText(ReadOnlyMemory<byte> content) => string.Join("\n", ReadParagraphs(content));

    public static IReadOnlyList<Run> ReadBodyRuns(ReadOnlyMemory<byte> content, int paragraphIndex) => Read(content, document =>
    {
        Paragraph paragraph = document.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ElementAt(paragraphIndex);
        return paragraph.Elements<Run>().Select(run => (Run)run.CloneNode(true)).ToList();
    });
}
