using DocumentRedaction.Core.Model;
using DocumentRedaction.Web.Api;

namespace DocumentRedaction.Web.Tests.Api;

public class RedactionRequestParserTests
{
    private static RedactionRequest Request(
        IReadOnlyList<string>? categories = null,
        IReadOnlyList<string>? excludedKinds = null,
        IReadOnlyList<string>? customTerms = null,
        string? placeholderFormat = null,
        bool caseSensitive = false) =>
        new(categories ?? ["all"], excludedKinds ?? [], customTerms ?? [], placeholderFormat, caseSensitive);

    [Fact]
    public void Defaults_to_every_category_and_default_placeholder()
    {
        RedactionRequestParser.ParseResult result = RedactionRequestParser.Parse(Request());
        Assert.True(result.IsValid);
        Assert.Equal(RedactionCategory.All, result.Options!.Categories);
        Assert.Equal(RedactionOptions.DefaultPlaceholderFormat, result.Options.PlaceholderFormat);
        Assert.Empty(result.Options.CustomTerms);
    }

    [Theory]
    [InlineData(new[] { "pii" }, RedactionCategory.Pii)]
    [InlineData(new[] { "PII", "Financial" }, RedactionCategory.Pii | RedactionCategory.Financial)]
    [InlineData(new[] { "pii, hipaa" }, RedactionCategory.Pii | RedactionCategory.Hipaa)]
    [InlineData(new[] { "pii;financial", "confidential" }, RedactionCategory.Pii | RedactionCategory.Financial | RedactionCategory.Confidential)]
    public void Parses_category_lists(string[] values, RedactionCategory expected) =>
        Assert.Equal(expected, RedactionRequestParser.Parse(Request(values)).Options!.Categories);

    [Fact]
    public void Parses_excluded_kinds_case_insensitively()
    {
        RedactionOptions options = RedactionRequestParser.Parse(Request(excludedKinds: ["date", "EmailAddress, PhoneNumber"])).Options!;
        Assert.Equal([InformationKind.EmailAddress, InformationKind.PhoneNumber, InformationKind.Date], options.ExcludedKinds.Order());
    }

    [Fact]
    public void Splits_custom_terms_on_line_breaks_only()
    {
        RedactionOptions options = RedactionRequestParser.Parse(Request(customTerms: ["Jane Doe\r\nAcme, Inc.\n\n  Falcon  "])).Options!;
        Assert.Equal(["Jane Doe", "Acme, Inc.", "Falcon"], options.CustomTerms);
    }

    [Fact]
    public void Custom_terms_alone_are_enough()
    {
        RedactionRequestParser.ParseResult result = RedactionRequestParser.Parse(Request(categories: [], customTerms: ["x"]));
        Assert.True(result.IsValid);
        Assert.Equal(RedactionCategory.None, result.Options!.Categories);
    }

    [Fact]
    public void Nothing_selected_is_an_error()
    {
        RedactionRequestParser.ParseResult result = RedactionRequestParser.Parse(Request(categories: []));
        Assert.False(result.IsValid);
        Assert.Contains("Categories", result.Errors.Keys);
    }

    [Fact]
    public void Blank_custom_terms_do_not_count()
    {
        RedactionRequestParser.ParseResult result = RedactionRequestParser.Parse(Request(categories: [], customTerms: ["  \n "]));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Collects_every_error()
    {
        RedactionRequestParser.ParseResult result = RedactionRequestParser.Parse(Request(["nope"], ["Nope"], placeholderFormat: "{1}"));
        Assert.False(result.IsValid);
        Assert.Equal(["Categories", "ExcludedKinds", "PlaceholderFormat"], result.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("<{0}>")]
    [InlineData("████")]
    [InlineData("[{0}]")]
    public void Accepts_valid_placeholder_formats(string format) =>
        Assert.Equal(format, RedactionRequestParser.Parse(Request(placeholderFormat: format)).Options!.PlaceholderFormat);

    [Theory]
    [InlineData("{0")]
    [InlineData("{1}")]
    [InlineData("}")]
    public void Rejects_invalid_placeholder_formats(string format) =>
        Assert.Contains("PlaceholderFormat", RedactionRequestParser.Parse(Request(placeholderFormat: format)).Errors.Keys);

    [Fact]
    public void Whitespace_placeholder_falls_back_to_default() =>
        Assert.Equal(RedactionOptions.DefaultPlaceholderFormat, RedactionRequestParser.Parse(Request(placeholderFormat: "  ")).Options!.PlaceholderFormat);

    [Fact]
    public void Case_sensitivity_flag_is_carried() =>
        Assert.True(RedactionRequestParser.Parse(Request(caseSensitive: true)).Options!.CustomTermsAreCaseSensitive);
}
