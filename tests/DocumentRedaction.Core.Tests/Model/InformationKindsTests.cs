using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;

namespace DocumentRedaction.Core.Tests.Model;

public class InformationKindsTests
{
    [Fact]
    public void Every_enum_value_is_catalogued() =>
        Assert.All(Enum.GetValues<InformationKind>(), kind => Assert.Equal(kind, InformationKinds.Get(kind).Kind));

    [Fact]
    public void Priorities_are_unique() =>
        Assert.Equal(InformationKinds.All.Count, InformationKinds.All.Select(info => info.Priority).Distinct().Count());

    [Fact]
    public void Placeholder_labels_are_unique_and_upper_case()
    {
        var labels = InformationKinds.All.Select(info => info.PlaceholderLabel).ToArray();
        Assert.Equal(labels.Length, labels.Distinct(StringComparer.Ordinal).Count());
        Assert.All(labels, label => Assert.Equal(label.ToUpperInvariant(), label));
    }

    [Fact]
    public void Only_custom_term_has_no_category() =>
        Assert.Equal([InformationKind.CustomTerm], InformationKinds.All.Where(info => info.Categories == RedactionCategory.None).Select(info => info.Kind));

    [Fact]
    public void Ssn_belongs_to_pii_and_hipaa()
    {
        var categories = InformationKinds.Get(InformationKind.SocialSecurityNumber).Categories;
        Assert.True(categories.HasFlag(RedactionCategory.Pii));
        Assert.True(categories.HasFlag(RedactionCategory.Hipaa));
        Assert.False(categories.HasFlag(RedactionCategory.Financial));
    }

    [Fact]
    public void Unknown_kind_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => InformationKinds.Get((InformationKind)999));

    [Fact]
    public void Only_the_confidential_statement_widens_to_a_sentence()
    {
        Assert.Equal([InformationKind.ConfidentialStatement], InformationKinds.SentenceKinds);
        Assert.True(InformationKinds.Get(InformationKind.ConfidentialStatement).WidensToSentence);
        Assert.False(InformationKinds.Get(InformationKind.CreditCardNumber).WidensToSentence);
    }

    [Fact]
    public void Sentence_kinds_never_cross_a_line_break()
    {
        // Processors rely on this flag to bound those detections by line; the detector must agree.
        const string text = "alpha confidential beta\ngamma delta\nproprietary epsilon";
        RedactionOptions options = new() { Categories = RedactionCategory.Confidential };
        IReadOnlyList<Detection> detections = TextRedactor.CreateDefault().Detect(text, options);

        Assert.Equal(2, detections.Count);
        Assert.All(detections, detection =>
        {
            Assert.Contains(detection.Kind, InformationKinds.SentenceKinds);
            Assert.DoesNotContain('\n', text.AsSpan(detection.Start, detection.Length).ToString());
        });
    }

    [Fact]
    public void Document_author_is_the_only_metadata_kind_and_belongs_to_pii_and_hipaa()
    {
        Assert.Equal([InformationKind.DocumentAuthor], InformationKinds.MetadataKinds);
        InformationKindInfo info = InformationKinds.Get(InformationKind.DocumentAuthor);
        Assert.True(info.MetadataOnly);
        Assert.Equal(RedactionCategory.Pii | RedactionCategory.Hipaa, info.Categories);
        Assert.Contains(InformationKind.DocumentAuthor, new RedactionOptions { Categories = RedactionCategory.Pii }.EffectiveKinds());
        Assert.DoesNotContain(InformationKind.DocumentAuthor, new RedactionOptions { Categories = RedactionCategory.Financial }.EffectiveKinds());
    }
}
