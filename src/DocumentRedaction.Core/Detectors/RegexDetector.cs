using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors;

/// <summary>
/// Base for detectors driven by a single regular expression. When the pattern defines a group
/// named <c>value</c>, only that group is reported so that keyword anchors such as
/// "Account No:" stay in the document. Override <see cref="IsValid"/> to add checksum rules.
/// </summary>
public abstract class RegexDetector : IDetector
{
    /// <summary>Name of the optional capture group that narrows the reported span.</summary>
    public const string ValueGroup = "value";

    public abstract InformationKind Kind { get; }

    protected abstract Regex Pattern { get; }

    public virtual IEnumerable<Detection> Detect(string text, RedactionOptions options)
    {
        foreach (Match match in Pattern.Matches(text))
        {
            Group span = match.Groups[ValueGroup] is { Success: true } value ? value : match;
            if (IsValid(span.Value))
            {
                yield return new Detection(Kind, span.Index, span.Length, span.Value);
            }
        }
    }

    /// <summary>Secondary validation of the matched value; return <c>false</c> to discard it.</summary>
    protected virtual bool IsValid(string value) => true;
}
