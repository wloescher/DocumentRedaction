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

    [Fact]
    public void No_api_keys_is_valid() =>
        Assert.True(_validator.Validate(null, new RedactionSettings()).Succeeded);

    [Theory]
    [InlineData("0123456789abcdef")]
    [InlineData("!~key-with_punctuation.and:colons@2026")]
    public void Sixteen_or_more_visible_ascii_characters_pass(string key) =>
        Assert.True(_validator.Validate(null, WithKeys(key)).Succeeded);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0123456789abcde")]
    public void Short_or_blank_key_fails_with_its_index_and_without_the_value(string key)
    {
        ValidateOptionsResult result = _validator.Validate(null, WithKeys("0123456789abcdef", key));
        Assert.True(result.Failed);
        Assert.Contains("Redaction:ApiKeys:1 must be at least 16 characters", result.FailureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("0123456789abcdef", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(" 0123456789abcdef")]
    [InlineData("0123456789abcdef\t")]
    [InlineData("0123456789 abcdef")]
    [InlineData("                    ")]
    [InlineData("0123456789abcdéf-key")]
    public void Key_with_whitespace_or_non_ascii_fails(string key)
    {
        ValidateOptionsResult result = _validator.Validate(null, WithKeys(key));
        Assert.True(result.Failed);
        Assert.Contains("Redaction:ApiKeys:0 must contain only visible ASCII characters", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Repeated_key_fails_at_its_second_position()
    {
        ValidateOptionsResult result = _validator.Validate(null, WithKeys("0123456789abcdef", "fedcba9876543210", "0123456789abcdef"));
        Assert.True(result.Failed);
        Assert.Contains("Redaction:ApiKeys:2 repeats an earlier key", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Rate_limit_defaults_are_valid()
    {
        RateLimitSettings rateLimit = new RedactionSettings().RateLimit;
        Assert.True(rateLimit.Enabled);
        Assert.Equal(60, rateLimit.PermitLimit);
        Assert.Equal(TimeSpan.FromSeconds(60), rateLimit.Window);
    }

    [Fact]
    public void Permit_limit_below_one_fails_with_the_key()
    {
        ValidateOptionsResult result = _validator.Validate(null, new RedactionSettings { RateLimit = new RateLimitSettings { PermitLimit = 0 } });
        Assert.True(result.Failed);
        Assert.Contains("Redaction:RateLimit:PermitLimit must be at least 1", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Window_below_one_second_fails_even_when_the_limiter_is_disabled()
    {
        // A disabled limiter with nonsense values would break the moment it is switched on.
        ValidateOptionsResult result = _validator.Validate(null, new RedactionSettings { RateLimit = new RateLimitSettings { Enabled = false, WindowSeconds = 0 } });
        Assert.True(result.Failed);
        Assert.Contains("Redaction:RateLimit:WindowSeconds must be at least 1", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Window_above_the_timer_ceiling_fails_and_the_ceiling_itself_passes()
    {
        ValidateOptionsResult tooLong = _validator.Validate(null, new RedactionSettings { RateLimit = new RateLimitSettings { WindowSeconds = RateLimitSettings.MaxWindowSeconds + 1 } });
        Assert.True(tooLong.Failed);
        Assert.Contains("Redaction:RateLimit:WindowSeconds must be at most 4,294,967", tooLong.FailureMessage, StringComparison.Ordinal);

        Assert.True(_validator.Validate(null, new RedactionSettings { RateLimit = new RateLimitSettings { WindowSeconds = RateLimitSettings.MaxWindowSeconds } }).Succeeded);
    }

    [Fact]
    public void Undefined_license_value_fails_naming_the_choices()
    {
        // The binder turns "99" into (QuestPdfLicense)99 without complaint.
        ValidateOptionsResult result = _validator.Validate(null, new RedactionSettings { QuestPdfLicense = (QuestPdfLicense)99 });
        Assert.True(result.Failed);
        Assert.Contains("Redaction:QuestPdfLicense must be one of Community, Professional, Enterprise", result.FailureMessage, StringComparison.Ordinal);
    }

    private static RedactionSettings WithKeys(params string[] keys)
    {
        RedactionSettings settings = new();
        foreach (string key in keys)
        {
            settings.ApiKeys.Add(key);
        }

        return settings;
    }
}
