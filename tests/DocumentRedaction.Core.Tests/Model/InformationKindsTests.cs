using DocumentRedaction.Core.Model;

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
}
