using System.Globalization;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Web.Api;

/// <summary>
/// Turns a <see cref="RedactionRequest"/> into <see cref="RedactionOptions"/>, collecting every
/// validation problem rather than stopping at the first. Shared by the API and the page so
/// both accept exactly the same input.
/// </summary>
public static class RedactionRequestParser
{
    private static readonly char[] ListSeparators = [',', ';'];
    private static readonly string[] LineSeparators = ["\r\n", "\n", "\r"];

    public sealed record ParseResult(RedactionOptions? Options, IReadOnlyDictionary<string, string[]> Errors)
    {
        public bool IsValid => Options is not null;
    }

    public static ParseResult Parse(RedactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Dictionary<string, List<string>> errors = new(StringComparer.Ordinal);

        RedactionCategory categories = ParseCategories(request.Categories, errors);
        HashSet<InformationKind> excluded = ParseKinds(request.ExcludedKinds, errors);
        string[] customTerms = SplitLines(request.CustomTerms);
        string placeholderFormat = ParsePlaceholderFormat(request.PlaceholderFormat, errors);

        if (categories == RedactionCategory.None && customTerms.Length == 0)
        {
            AddError(errors, nameof(request.Categories), "Select at least one category or supply a custom term.");
        }

        if (errors.Count > 0)
        {
            return new ParseResult(null, errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal));
        }

        RedactionOptions options = new()
        {
            Categories = categories,
            ExcludedKinds = excluded,
            CustomTerms = customTerms,
            CustomTermsAreCaseSensitive = request.CustomTermsAreCaseSensitive,
            PlaceholderFormat = placeholderFormat,
        };
        return new ParseResult(options, new Dictionary<string, string[]>(StringComparer.Ordinal));
    }

    private static RedactionCategory ParseCategories(IEnumerable<string> values, Dictionary<string, List<string>> errors)
    {
        RedactionCategory categories = RedactionCategory.None;
        foreach (string value in SplitList(values))
        {
            if (RedactionCategories.TryParse(value, out RedactionCategory category))
            {
                categories |= category;
            }
            else
            {
                AddError(errors, nameof(RedactionRequest.Categories), $"Unknown category '{value}'. Expected one of: {string.Join(", ", RedactionCategories.All.Select(info => info.Category))}, all.");
            }
        }

        return categories;
    }

    private static HashSet<InformationKind> ParseKinds(IEnumerable<string> values, Dictionary<string, List<string>> errors)
    {
        HashSet<InformationKind> kinds = [];
        foreach (string value in SplitList(values))
        {
            if (Enum.TryParse(value, ignoreCase: true, out InformationKind kind) && Enum.IsDefined(kind))
            {
                kinds.Add(kind);
            }
            else
            {
                AddError(errors, nameof(RedactionRequest.ExcludedKinds), $"Unknown information kind '{value}'.");
            }
        }

        return kinds;
    }

    private static string ParsePlaceholderFormat(string? value, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return RedactionOptions.DefaultPlaceholderFormat;
        }

        try
        {
            _ = string.Format(CultureInfo.InvariantCulture, value, "LABEL");
            return value;
        }
        catch (FormatException)
        {
            AddError(errors, nameof(RedactionRequest.PlaceholderFormat), "Placeholder format must be a valid composite format such as \"[REDACTED-{0}]\"; only {0} may be used.");
            return RedactionOptions.DefaultPlaceholderFormat;
        }
    }

    private static IEnumerable<string> SplitList(IEnumerable<string> values) =>
        values.SelectMany(value => value.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string[] SplitLines(IEnumerable<string> values) =>
        values.SelectMany(value => value.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray();

    private static void AddError(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out List<string>? list))
        {
            list = [];
            errors[field] = list;
        }

        list.Add(message);
    }
}
