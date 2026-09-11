namespace DocumentRedaction.Documents.Tests;

public class DocumentLimitsTests
{
    [Fact]
    public void Defaults_are_generous_and_valid()
    {
        DocumentLimits limits = new();
        limits.Validate();
        Assert.Equal(64L * 1024 * 1024, limits.MaxDecodedBytes);
        Assert.Equal(1024L * 1024 * 1024, limits.MaxTotalDecodedBytes);
        Assert.Equal(50_000_000, limits.MaxTextCharacters);
        Assert.Equal(2_000, limits.MaxPdfPages);
    }

    [Fact]
    public void Minimum_of_one_is_accepted() =>
        new DocumentLimits { MaxDecodedBytes = 1, MaxTotalDecodedBytes = 1, MaxTextCharacters = 1, MaxPdfPages = 1 }.Validate();

    public static TheoryData<DocumentLimits, string> Invalid => new()
    {
        { new DocumentLimits { MaxDecodedBytes = 0 }, nameof(DocumentLimits.MaxDecodedBytes) },
        { new DocumentLimits { MaxTotalDecodedBytes = -5 }, nameof(DocumentLimits.MaxTotalDecodedBytes) },
        { new DocumentLimits { MaxTextCharacters = 0 }, nameof(DocumentLimits.MaxTextCharacters) },
        { new DocumentLimits { MaxPdfPages = 0 }, nameof(DocumentLimits.MaxPdfPages) },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Validate_names_the_offending_property(DocumentLimits limits, string property)
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(limits.Validate);
        Assert.Contains(property, ex.Message, StringComparison.Ordinal);
        Assert.False(limits.TryValidate(out string? error));
        Assert.Equal(ex.Message, error);
    }

    [Fact]
    public void Try_validate_reports_the_first_failure_only()
    {
        Assert.False(new DocumentLimits { MaxDecodedBytes = 0, MaxPdfPages = 0 }.TryValidate(out string? error));
        Assert.Contains(nameof(DocumentLimits.MaxDecodedBytes), error, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(DocumentLimits.MaxPdfPages), error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validated_returns_defaults_for_null_and_the_instance_otherwise()
    {
        DocumentLimits limits = new() { MaxPdfPages = 3 };
        Assert.Same(limits, DocumentLimits.Validated(limits));
        Assert.Equal(DocumentLimits.DefaultMaxPdfPages, DocumentLimits.Validated(null).MaxPdfPages);
        Assert.Throws<InvalidOperationException>(() => DocumentLimits.Validated(new DocumentLimits { MaxTextCharacters = 0 }));
    }

    [Fact]
    public void At_least_one_shares_its_message()
    {
        Assert.Null(DocumentLimits.AtLeastOne(1, "Any"));
        Assert.Equal("Any must be at least 1 but is -2.", DocumentLimits.AtLeastOne(-2, "Any"));
    }
}
