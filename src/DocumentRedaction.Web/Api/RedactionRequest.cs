using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Web.Api;

/// <summary>Raw user input for a redaction run, before validation, as it arrives from a form or the page.</summary>
/// <param name="Categories">Category names; each value may itself be a comma-separated list.</param>
/// <param name="ExcludedKinds">Kind names to skip; each value may itself be a comma-separated list.</param>
/// <param name="CustomTerms">Literal terms; each value may contain several terms separated by line breaks.</param>
/// <param name="PlaceholderFormat">Optional composite format, defaulting to <see cref="RedactionOptions.DefaultPlaceholderFormat"/>.</param>
/// <param name="CustomTermsAreCaseSensitive">Whether custom terms must match case.</param>
public sealed record RedactionRequest(
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> ExcludedKinds,
    IReadOnlyList<string> CustomTerms,
    string? PlaceholderFormat,
    bool CustomTermsAreCaseSensitive);
