using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>
/// Literal words or phrases supplied by the user. Word boundaries are applied at each end that
/// starts or ends with a word character, so "Ann" does not match "Annual". The compiled pattern
/// is cached per <see cref="RedactionOptions"/> instance because document processors call
/// <see cref="Detect"/> once per paragraph with the same options.
/// </summary>
public sealed class CustomTermDetector : IDetector
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(5);
    private readonly ConditionalWeakTable<RedactionOptions, Regex> _cache = [];

    public InformationKind Kind => InformationKind.CustomTerm;

    public IEnumerable<Detection> Detect(string text, RedactionOptions options)
    {
        if (options.NormalizedCustomTerms.Count == 0)
        {
            return [];
        }

        Regex pattern = _cache.GetValue(options, BuildRegex);
        return pattern.Matches(text)
            .Select(match => new Detection(Kind, match.Index, match.Length, match.Value))
            .ToArray();
    }

    internal static string BuildPattern(IEnumerable<string> terms)
    {
        // Longest first so that "New York City" wins over "New York" at the same position.
        IEnumerable<string> alternatives = terms
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(term => term.Length)
            .Select(term =>
            {
                string escaped = Regex.Escape(term).Replace(@"\ ", @"\s+", StringComparison.Ordinal);
                string prefix = char.IsLetterOrDigit(term[0]) ? @"\b" : string.Empty;
                string suffix = char.IsLetterOrDigit(term[^1]) ? @"\b" : string.Empty;
                return prefix + escaped + suffix;
            });

        return string.Join("|", alternatives);
    }

    private static Regex BuildRegex(RedactionOptions options)
    {
        RegexOptions regexOptions = RegexOptions.CultureInvariant
            | (options.CustomTermsAreCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
        return new Regex(BuildPattern(options.NormalizedCustomTerms), regexOptions, MatchTimeout);
    }
}
