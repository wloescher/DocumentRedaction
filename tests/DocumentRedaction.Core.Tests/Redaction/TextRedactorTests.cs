using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;

namespace DocumentRedaction.Core.Tests.Redaction;

public class TextRedactorTests
{
    private readonly TextRedactor _redactor = TextRedactor.CreateDefault();

    private const string Sample =
        "Patient John Smith, SSN 123-45-6789, DOB 01/02/1980, phone (555) 123-4567, " +
        "email john@example.com, card 4111 1111 1111 1111, IBAN GB82 WEST 1234 5698 7654 32. " +
        "This report is confidential.";

    [Fact]
    public void Redacts_everything_by_default()
    {
        var result = _redactor.Redact(Sample, new RedactionOptions());

        Assert.Equal(
            "Patient John Smith, SSN [REDACTED-SSN], DOB [REDACTED-DATE], phone [REDACTED-PHONE], " +
            "email [REDACTED-EMAIL], card [REDACTED-CREDIT-CARD], IBAN [REDACTED-IBAN]. " +
            "[REDACTED-CONFIDENTIAL]",
            result.RedactedText);
        Assert.Equal(7, result.Report.Total);
        Assert.Equal(1, result.Report.CountsByKind[InformationKind.SocialSecurityNumber]);
    }

    [Fact]
    public void Only_selected_categories_are_redacted()
    {
        var result = _redactor.Redact(Sample, new RedactionOptions { Categories = RedactionCategory.Financial });

        Assert.Contains("123-45-6789", result.RedactedText, StringComparison.Ordinal);
        Assert.Contains("john@example.com", result.RedactedText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED-CREDIT-CARD]", result.RedactedText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED-IBAN]", result.RedactedText, StringComparison.Ordinal);
        Assert.Equal(2, result.Report.Total);
    }

    [Fact]
    public void Hipaa_includes_pii_kinds_and_dates()
    {
        var result = _redactor.Redact(Sample, new RedactionOptions { Categories = RedactionCategory.Hipaa });

        Assert.Contains("[REDACTED-SSN]", result.RedactedText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED-DATE]", result.RedactedText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED-EMAIL]", result.RedactedText, StringComparison.Ordinal);
        Assert.Contains("4111 1111 1111 1111", result.RedactedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Pii_alone_leaves_dates()
    {
        var result = _redactor.Redact(Sample, new RedactionOptions { Categories = RedactionCategory.Pii });
        Assert.Contains("01/02/1980", result.RedactedText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED-SSN]", result.RedactedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Excluded_kinds_are_skipped()
    {
        var options = new RedactionOptions { ExcludedKinds = new HashSet<InformationKind> { InformationKind.Date } };
        var result = _redactor.Redact(Sample, options);
        Assert.Contains("01/02/1980", result.RedactedText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED-SSN]", result.RedactedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Custom_terms_apply_even_with_no_categories()
    {
        var options = new RedactionOptions { Categories = RedactionCategory.None, CustomTerms = ["John Smith"] };
        var result = _redactor.Redact(Sample, options);
        Assert.StartsWith("Patient [REDACTED-CUSTOM], SSN 123-45-6789", result.RedactedText, StringComparison.Ordinal);
        Assert.Equal(1, result.Report.Total);
    }

    [Fact]
    public void Nothing_selected_returns_text_unchanged()
    {
        var result = _redactor.Redact(Sample, new RedactionOptions { Categories = RedactionCategory.None });
        Assert.Equal(Sample, result.RedactedText);
        Assert.Empty(result.Detections);
        Assert.Equal(0, result.Report.Total);
    }

    [Fact]
    public void Custom_placeholder_format_is_used()
    {
        var options = new RedactionOptions { Categories = RedactionCategory.Pii, PlaceholderFormat = "<{0}>" };
        var result = _redactor.Redact("mail a@b.co", options);
        Assert.Equal("mail <EMAIL>", result.RedactedText);
    }

    [Fact]
    public void Fixed_block_placeholder_without_label_is_allowed()
    {
        var options = new RedactionOptions { PlaceholderFormat = "████" };
        Assert.Equal("mail ████", _redactor.Redact("mail a@b.co", options).RedactedText);
    }

    [Fact]
    public void Empty_text_is_a_no_op()
    {
        var result = _redactor.Redact(string.Empty, new RedactionOptions());
        Assert.Equal(string.Empty, result.RedactedText);
        Assert.Empty(result.Detections);
    }

    [Fact]
    public void Overlapping_candidates_are_resolved_before_apply()
    {
        // The confidential sentence swallows the email inside it.
        var result = _redactor.Redact("Confidential: reach a@b.co now.", new RedactionOptions());
        Assert.Equal("[REDACTED-CONFIDENTIAL]", result.RedactedText);
        Assert.Single(result.Detections);
    }

    [Fact]
    public void Detections_are_ordered_and_offsets_match_text()
    {
        var detections = _redactor.Detect(Sample, new RedactionOptions());
        Assert.Equal(detections.OrderBy(d => d.Start), detections);
        Assert.All(detections, d => Assert.Equal(d.Text, Sample.Substring(d.Start, d.Length)));
    }

    [Fact]
    public void Apply_rejects_overlapping_detections()
    {
        Detection[] bad =
        [
            new(InformationKind.EmailAddress, 0, 5, "xxxxx"),
            new(InformationKind.EmailAddress, 3, 5, "xxxxx"),
        ];
        Assert.Throws<ArgumentException>(() => _redactor.Apply("0123456789", bad, new RedactionOptions()));
    }

    [Fact]
    public void Apply_rejects_detections_outside_text() =>
        Assert.Throws<ArgumentException>(() =>
            _redactor.Apply("short", [new Detection(InformationKind.EmailAddress, 3, 5, "xxxxx")], new RedactionOptions()));

    [Fact]
    public void Apply_with_no_detections_returns_same_instance()
    {
        const string text = "unchanged";
        Assert.Same(text, _redactor.Apply(text, [], new RedactionOptions()));
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => _redactor.Detect(null!, new RedactionOptions()));
        Assert.Throws<ArgumentNullException>(() => _redactor.Detect("x", null!));
    }

    [Fact]
    public void Unknown_kind_in_options_without_detector_is_ignored()
    {
        var limited = new TextRedactor([new Core.Detectors.Builtin.EmailAddressDetector()]);
        var result = limited.Redact("a@b.co 123-45-6789", new RedactionOptions());
        Assert.Equal("[REDACTED-EMAIL] 123-45-6789", result.RedactedText);
    }
}
