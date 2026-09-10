using System.Globalization;

namespace DocumentRedaction.Core.Model;

/// <summary>User-selected settings for a redaction run.</summary>
public sealed record RedactionOptions
{
    public const string DefaultPlaceholderFormat = "[REDACTED-{0}]";

    /// <summary>Categories to redact. Every kind in any selected category is enabled.</summary>
    public RedactionCategory Categories { get; init; } = RedactionCategory.All;

    /// <summary>Kinds to leave alone even though their category is selected (for example, dates).</summary>
    public IReadOnlySet<InformationKind> ExcludedKinds { get; init; } = new HashSet<InformationKind>();

    /// <summary>Literal words or phrases to redact regardless of category selection.</summary>
    public IReadOnlyList<string> CustomTerms { get; init; } = [];

    public bool CustomTermsAreCaseSensitive { get; init; }

    /// <summary>
    /// Composite format for replacement text. <c>{0}</c> receives the kind's placeholder label.
    /// </summary>
    public string PlaceholderFormat { get; init; } = DefaultPlaceholderFormat;

    /// <summary>Custom terms with blanks removed and surrounding whitespace trimmed.</summary>
    public IReadOnlyList<string> NormalizedCustomTerms =>
        CustomTerms.Select(term => term.Trim()).Where(term => term.Length > 0).ToArray();

    /// <summary>
    /// The kinds that will actually be detected: every kind in a selected category minus
    /// <see cref="ExcludedKinds"/>, plus <see cref="InformationKind.CustomTerm"/> when any custom
    /// term is supplied.
    /// </summary>
    public IReadOnlySet<InformationKind> EffectiveKinds()
    {
        HashSet<InformationKind> kinds = InformationKinds.ForCategories(Categories)
            .Select(info => info.Kind)
            .Where(kind => !ExcludedKinds.Contains(kind))
            .ToHashSet();

        if (NormalizedCustomTerms.Count > 0)
        {
            kinds.Add(InformationKind.CustomTerm);
        }

        return kinds;
    }

    public string PlaceholderFor(InformationKind kind) =>
        string.Format(CultureInfo.InvariantCulture, PlaceholderFormat, InformationKinds.Get(kind).PlaceholderLabel);
}
