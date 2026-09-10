using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using V = DocumentFormat.OpenXml.Vml;

namespace DocumentRedaction.Tests.Fixtures;

/// <summary>Builds small .docx files in memory so tests need no binary fixtures.</summary>
public static class WordFixture
{
    public static byte[] Build(Action<MainDocumentPart> configure)
    {
        using MemoryStream stream = new();
        using (WordprocessingDocument document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart main = document.AddMainDocumentPart();
            main.Document = new Document(new Body());
            configure(main);
            main.Document!.Save();
        }

        return stream.ToArray();
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

    public static IReadOnlyList<string> ReadHyperlinkTargets(ReadOnlyMemory<byte> content)
    {
        using MemoryStream stream = new(content.ToArray());
        using WordprocessingDocument document = WordprocessingDocument.Open(stream, isEditable: false);
        return document.MainDocumentPart!.HyperlinkRelationships.Select(link => link.Uri.OriginalString).ToList();
    }

    public static IReadOnlyList<string> ReadFieldCodes(ReadOnlyMemory<byte> content)
    {
        using MemoryStream stream = new(content.ToArray());
        using WordprocessingDocument document = WordprocessingDocument.Open(stream, isEditable: false);
        return document.MainDocumentPart!.Document!.Body!.Descendants<FieldCode>().Select(code => code.Text).ToList();
    }

    private static void AddSectionReference(MainDocumentPart main, OpenXmlElement reference)
    {
        Body body = main.Document!.Body!;
        SectionProperties section = body.Elements<SectionProperties>().FirstOrDefault() ?? body.AppendChild(new SectionProperties());
        section.PrependChild(reference);
    }

    /// <summary>All text in the document, one entry per paragraph, across every text-bearing part.</summary>
    public static IReadOnlyList<string> ReadParagraphs(ReadOnlyMemory<byte> content)
    {
        using MemoryStream stream = new(content.ToArray());
        using WordprocessingDocument document = WordprocessingDocument.Open(stream, isEditable: false);
        MainDocumentPart main = document.MainDocumentPart!;
        IEnumerable<OpenXmlPart> parts = [main, .. main.HeaderParts, .. main.FooterParts];
        if (main.FootnotesPart is { } footnotes)
        {
            parts = parts.Append(footnotes);
        }

        if (main.WordprocessingCommentsPart is { } comments)
        {
            parts = parts.Append(comments);
        }

        return parts.SelectMany(part => part.RootElement!.Descendants<Paragraph>()).Select(p => p.InnerText).ToList();
    }

    public static string ReadAllText(ReadOnlyMemory<byte> content) => string.Join("\n", ReadParagraphs(content));

    public static IReadOnlyList<Run> ReadBodyRuns(ReadOnlyMemory<byte> content, int paragraphIndex)
    {
        using MemoryStream stream = new(content.ToArray());
        using WordprocessingDocument document = WordprocessingDocument.Open(stream, isEditable: false);
        Paragraph paragraph = document.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ElementAt(paragraphIndex);
        return paragraph.Elements<Run>().Select(run => (Run)run.CloneNode(true)).ToList();
    }
}
