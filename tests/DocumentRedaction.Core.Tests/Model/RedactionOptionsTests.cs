using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Tests.Model;

public class RedactionOptionsTests
{
    [Fact]
    public void Default_enables_every_kind_except_custom_terms()
    {
        var kinds = new RedactionOptions().EffectiveKinds();
        Assert.DoesNotContain(InformationKind.CustomTerm, kinds);
        Assert.Equal(Enum.GetValues<InformationKind>().Length - 1, kinds.Count);
    }

    [Fact]
    public void Custom_terms_enable_the_custom_kind()
    {
        var options = new RedactionOptions { Categories = RedactionCategory.None, CustomTerms = [" secret "] };
        Assert.Equal([InformationKind.CustomTerm], options.EffectiveKinds());
        Assert.Equal(["secret"], options.NormalizedCustomTerms);
    }

    [Fact]
    public void Financial_selects_only_financial_kinds()
    {
        var kinds = new RedactionOptions { Categories = RedactionCategory.Financial }.EffectiveKinds();
        Assert.All(kinds, kind => Assert.True(InformationKinds.Get(kind).Categories.HasFlag(RedactionCategory.Financial)));
        Assert.Contains(InformationKind.CreditCardNumber, kinds);
        Assert.DoesNotContain(InformationKind.EmailAddress, kinds);
    }

    [Fact]
    public void Excluded_kinds_are_removed()
    {
        var options = new RedactionOptions { ExcludedKinds = new HashSet<InformationKind> { InformationKind.Date, InformationKind.EmailAddress } };
        var kinds = options.EffectiveKinds();
        Assert.DoesNotContain(InformationKind.Date, kinds);
        Assert.DoesNotContain(InformationKind.EmailAddress, kinds);
    }

    [Theory]
    [InlineData(InformationKind.SocialSecurityNumber, "[REDACTED-SSN]")]
    [InlineData(InformationKind.CreditCardNumber, "[REDACTED-CREDIT-CARD]")]
    public void Placeholder_uses_kind_label(InformationKind kind, string expected) =>
        Assert.Equal(expected, new RedactionOptions().PlaceholderFor(kind));
}
