using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>
/// Finds confidentiality markers and widens each hit to the surrounding sentence, so the whole
/// statement ("This report is confidential and ...") is replaced rather than the single word.
/// A sentence runs from the previous terminator or line break to the next.
/// </summary>
public sealed partial class ConfidentialStatementDetector : IDetector
{
    public InformationKind Kind => InformationKind.ConfidentialStatement;

    public IEnumerable<Detection> Detect(string text, RedactionOptions options)
    {
        int lastEnd = -1;
        foreach (Match match in MarkerRegex().Matches(text))
        {
            (int start, int end) = SentenceBounds(text, match.Index, match.Index + match.Length);
            if (start < lastEnd)
            {
                // Two markers in the same sentence produce one detection.
                continue;
            }

            lastEnd = end;
            yield return new Detection(Kind, start, end - start, text[start..end]);
        }
    }

    private static (int Start, int End) SentenceBounds(string text, int markerStart, int markerEnd)
    {
        int start = markerStart;
        while (start > 0 && !IsSentenceBreak(text, start - 1))
        {
            start--;
        }

        while (start < markerStart && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        int end = markerEnd;
        while (end < text.Length && !IsSentenceBreak(text, end))
        {
            end++;
        }

        if (end < text.Length && text[end] is '.' or '!' or '?')
        {
            end++;
        }

        return (start, end);
    }

    /// <summary>
    /// A line break always ends a sentence. A terminator ends one only when followed by
    /// whitespace or the end of the text, so "a@b.co" and "3.5" stay inside their sentence.
    /// </summary>
    private static bool IsSentenceBreak(string text, int index)
    {
        char c = text[index];
        if (c is '\n' or '\r')
        {
            return true;
        }

        if (c is '.' or '!' or '?')
        {
            return index + 1 == text.Length || char.IsWhiteSpace(text[index + 1]);
        }

        return false;
    }

    [GeneratedRegex(
        @"\b(?:confidential(?:ity)?|proprietary|internal\s+use\s+only|trade\s+secrets?|do\s+not\s+distribute|not\s+for\s+distribution|privileged\s+(?:and|&)\s+confidential|attorney[- ]client\s+privileged?)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex MarkerRegex();
}
