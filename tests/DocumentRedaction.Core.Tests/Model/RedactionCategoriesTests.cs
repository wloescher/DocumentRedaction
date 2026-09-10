using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Tests.Model;

public class RedactionCategoriesTests
{
    [Fact]
    public void Every_single_flag_is_described()
    {
        RedactionCategory[] flags = [RedactionCategory.Pii, RedactionCategory.Hipaa, RedactionCategory.Financial, RedactionCategory.Confidential];
        Assert.Equal(flags, RedactionCategories.All.Select(info => info.Category));
        Assert.All(flags, flag => Assert.Equal(flag, RedactionCategories.Get(flag).Category));
    }

    [Fact]
    public void Kinds_follow_the_kind_catalog() =>
        Assert.Contains(InformationKind.SocialSecurityNumber, RedactionCategories.Get(RedactionCategory.Hipaa).Kinds.Select(k => k.Kind));

    [Theory]
    [InlineData("pii", RedactionCategory.Pii)]
    [InlineData("HIPAA", RedactionCategory.Hipaa)]
    [InlineData("HIPAA / PHI", RedactionCategory.Hipaa)]
    [InlineData("financial", RedactionCategory.Financial)]
    [InlineData("Confidential", RedactionCategory.Confidential)]
    [InlineData("ALL", RedactionCategory.All)]
    public void Parses_names(string value, RedactionCategory expected)
    {
        Assert.True(RedactionCategories.TryParse(value, out RedactionCategory category));
        Assert.Equal(expected, category);
    }

    [Theory]
    [InlineData("")]
    [InlineData("none")]
    [InlineData("secret")]
    public void Rejects_unknown(string value) => Assert.False(RedactionCategories.TryParse(value, out _));

    [Fact]
    public void Composite_flag_has_no_single_entry() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RedactionCategories.Get(RedactionCategory.All));
}
