using DocumentRedaction.Documents;
using Microsoft.Extensions.Options;

namespace DocumentRedaction.Web.Tests;

public class RedactionSettingsValidatorTests
{
    private readonly RedactionSettingsValidator _validator = new();

    [Fact]
    public void Defaults_are_valid() =>
        Assert.True(_validator.Validate(null, new RedactionSettings()).Succeeded);

    [Fact]
    public void Upload_limit_below_one_fails_with_the_key()
    {
        ValidateOptionsResult result = _validator.Validate(null, new RedactionSettings { MaxUploadBytes = 0 });
        Assert.True(result.Failed);
        Assert.Contains("Redaction:MaxUploadBytes", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Upload_limit_above_the_array_ceiling_fails()
    {
        ValidateOptionsResult result = _validator.Validate(null, new RedactionSettings { MaxUploadBytes = RedactionSettings.MaxUploadBytesCeiling + 1 });
        Assert.True(result.Failed);
        Assert.Contains("Redaction:MaxUploadBytes must be at most", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Upload_limit_at_the_ceiling_passes_and_the_body_limit_stays_positive()
    {
        RedactionSettings settings = new() { MaxUploadBytes = RedactionSettings.MaxUploadBytesCeiling };
        Assert.True(_validator.Validate(null, settings).Succeeded);
        Assert.Equal(int.MaxValue, settings.RequestBodyLimit);
    }

    [Fact]
    public void Invalid_document_limits_fail_with_the_property()
    {
        ValidateOptionsResult result = _validator.Validate(null, new RedactionSettings { Limits = new DocumentLimits { MaxTotalDecodedBytes = -1 } });
        Assert.True(result.Failed);
        Assert.Contains("Redaction:Limits:MaxTotalDecodedBytes", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Request_body_limit_adds_64_kib_of_slack() =>
        Assert.Equal(1_000 + 65_536, new RedactionSettings { MaxUploadBytes = 1_000 }.RequestBodyLimit);
}
