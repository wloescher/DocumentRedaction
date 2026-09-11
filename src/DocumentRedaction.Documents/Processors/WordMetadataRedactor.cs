using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.VariantTypes;
using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;

namespace DocumentRedaction.Documents.Processors;

/// <summary>
/// Redacts what a .docx says about people outside its text: the creator and last editor in the
/// core properties, the manager in the application properties, the author and initials on
/// comments and tracked changes in any part, and the people part that lists reviewers. Those
/// are all names by definition, so when <see cref="InformationKind.DocumentAuthor"/> is selected
/// they are replaced wholesale. Free-text properties (title, subject, keywords, description,
/// company, string custom properties) go through the ordinary text detectors. Embedded objects,
/// imported chunks and SmartArt are not opened; they are counted and reported as warnings.
/// One instance serves one document.
/// </summary>
internal sealed class WordMetadataRedactor
{
    private const string WordprocessingNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private readonly ITextRedactor _redactor;
    private readonly RedactionOptions _options;
    private readonly bool _redactAuthors;
    private readonly string _authorPlaceholder;
    private RedactionReport _report = RedactionReport.Empty;
    private int _authorCount;

    public WordMetadataRedactor(ITextRedactor redactor, RedactionOptions options)
    {
        _redactor = redactor;
        _options = options;
        _redactAuthors = options.EffectiveKinds().Contains(InformationKind.DocumentAuthor);
        _authorPlaceholder = options.PlaceholderFor(InformationKind.DocumentAuthor);
    }

    public sealed record Result(RedactionReport Report, IReadOnlyList<string> Warnings);

    public Result Redact(WordprocessingDocument document, CancellationToken cancellationToken)
    {
        RedactCoreProperties(document);
        RedactExtendedProperties(document);
        RedactCustomProperties(document);

        // Authors appear wherever changes are tracked, including styles, numbering and the
        // glossary, so every XML part in the package is scanned rather than a curated list.
        List<OpenXmlPart> parts = document.GetAllParts().ToList();
        if (_redactAuthors)
        {
            foreach (OpenXmlPart part in parts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (part.RootElement is { } root)
                {
                    RedactAuthorAttributes(root);
                }
            }

            RemovePeoplePart(document);
        }

        RedactionReport authors = _authorCount == 0
            ? RedactionReport.Empty
            : new RedactionReport(new Dictionary<InformationKind, int> { [InformationKind.DocumentAuthor] = _authorCount });
        return new Result(_report.Merge(authors), Warnings(parts));
    }

    private void RedactCoreProperties(WordprocessingDocument document)
    {
        var properties = document.PackageProperties;
        properties.Creator = Author(properties.Creator);
        properties.LastModifiedBy = Author(properties.LastModifiedBy);
        properties.Title = Text(properties.Title);
        properties.Subject = Text(properties.Subject);
        properties.Keywords = Text(properties.Keywords);
        properties.Description = Text(properties.Description);
        properties.Category = Text(properties.Category);
        properties.ContentStatus = Text(properties.ContentStatus);
    }

    private void RedactExtendedProperties(WordprocessingDocument document)
    {
        if (document.ExtendedFilePropertiesPart?.Properties is not { } properties)
        {
            return;
        }

        if (properties.Manager is { } manager)
        {
            manager.Text = Author(manager.Text) ?? string.Empty;
        }

        if (properties.Company is { } company)
        {
            company.Text = Text(company.Text) ?? string.Empty;
        }
    }

    /// <summary>Custom properties carry typed values; only the string-typed ones can hold text worth redacting.</summary>
    private void RedactCustomProperties(WordprocessingDocument document)
    {
        if (document.CustomFilePropertiesPart?.Properties is not { } properties)
        {
            return;
        }

        foreach (CustomDocumentProperty property in properties.Elements<CustomDocumentProperty>())
        {
            foreach (OpenXmlLeafTextElement value in property.Elements().Where(value => value is VTLPWSTR or VTBString).Cast<OpenXmlLeafTextElement>())
            {
                value.Text = Text(value.Text) ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// Every element that names a person does so through a w:author attribute (comments, inserted
    /// and deleted runs, property changes, moves), so the attribute is handled generically rather
    /// than per element type. Initials identify the same person and are dropped.
    /// </summary>
    private void RedactAuthorAttributes(OpenXmlElement root)
    {
        foreach (OpenXmlElement element in root.Descendants())
        {
            if (!element.HasAttributes)
            {
                continue;
            }

            OpenXmlAttribute? author = null;
            bool hasInitials = false;
            foreach (OpenXmlAttribute attribute in element.GetAttributes())
            {
                if (attribute.NamespaceUri != WordprocessingNamespace)
                {
                    continue;
                }

                if (attribute.LocalName == "author" && !string.IsNullOrEmpty(attribute.Value))
                {
                    author = attribute;
                }
                else if (attribute.LocalName == "initials")
                {
                    hasInitials = true;
                }
            }

            if (author is not { } found)
            {
                continue;
            }

            _authorCount++;
            element.SetAttribute(new OpenXmlAttribute(found.Prefix, found.LocalName, found.NamespaceUri, _authorPlaceholder));
            if (hasInitials)
            {
                element.RemoveAttribute("initials", WordprocessingNamespace);
            }
        }
    }

    private static void RemovePeoplePart(WordprocessingDocument document)
    {
        if (document.MainDocumentPart is not { } main)
        {
            return;
        }

        foreach (WordprocessingPeoplePart people in main.GetPartsOfType<WordprocessingPeoplePart>().ToList())
        {
            main.DeletePart(people);
        }
    }

    /// <summary>
    /// Content the service does not look inside: embedded files, and imported HTML/RTF chunks or
    /// SmartArt whose text lives in formats the text pass does not read. Each is named so nobody
    /// assumes it was scanned.
    /// </summary>
    private static List<string> Warnings(List<OpenXmlPart> parts)
    {
        List<string> warnings = [];
        int embedded = parts.Count(part => part is EmbeddedObjectPart or EmbeddedPackagePart);
        if (embedded > 0)
        {
            warnings.Add($"{embedded} embedded object{(embedded == 1 ? " was" : "s were")} left unchanged; the service does not open embedded files, so their contents were not redacted.");
        }

        int imported = parts.Count(part => part is AlternativeFormatImportPart or DiagramDataPart);
        if (imported > 0)
        {
            warnings.Add($"{imported} imported-content or SmartArt part{(imported == 1 ? " was" : "s were")} left unchanged; text inside HTML/RTF imports and SmartArt is not redacted.");
        }

        return warnings;
    }

    private string? Author(string? value)
    {
        if (!_redactAuthors || string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        _authorCount++;
        return _authorPlaceholder;
    }

    private string? Text(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        TextRedactionResult result = _redactor.Redact(value, _options);
        _report = _report.Merge(result.Report);
        return result.RedactedText;
    }
}
