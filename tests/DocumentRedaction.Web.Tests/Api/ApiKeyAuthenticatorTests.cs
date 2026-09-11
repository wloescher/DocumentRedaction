using DocumentRedaction.Web.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DocumentRedaction.Web.Tests.Api;

public class ApiKeyAuthenticatorTests
{
    private const string First = "first-key-0123456789";
    private const string Second = "second-key-0123456789";

    private static ApiKeyAuthenticator Authenticator(params string[] keys)
    {
        RedactionSettings settings = new();
        foreach (string key in keys)
        {
            settings.ApiKeys.Add(key);
        }

        return new ApiKeyAuthenticator(Options.Create(settings));
    }

    private static DefaultHttpContext Request(params string[] headerValues)
    {
        DefaultHttpContext context = new();
        if (headerValues.Length > 0)
        {
            context.Request.Headers[ApiKeyAuthenticator.HeaderName] = headerValues;
        }

        return context;
    }

    [Fact]
    public void Open_mode_accepts_any_request_without_a_key_index()
    {
        ApiKeyAuthenticator authenticator = Authenticator();
        Assert.True(authenticator.IsOpen);
        Assert.True(authenticator.TryAuthenticate(Request(), out int index));
        Assert.Equal(-1, index);
        Assert.True(authenticator.TryAuthenticate(Request("anything"), out _));
    }

    [Theory]
    [InlineData(First, 0)]
    [InlineData(Second, 1)]
    public void Matching_key_is_accepted_with_its_position(string presented, int expectedIndex)
    {
        ApiKeyAuthenticator authenticator = Authenticator(First, Second);
        Assert.False(authenticator.IsOpen);
        Assert.True(authenticator.TryAuthenticate(Request(presented), out int index));
        Assert.Equal(expectedIndex, index);
    }

    [Theory]
    [InlineData("wrong-key-0123456789")]
    [InlineData("FIRST-KEY-0123456789")]
    [InlineData("first-key-012345678")]
    [InlineData("first-key-0123456789 ")]
    [InlineData("   ")]
    [InlineData(First + "," + Second)]
    [InlineData("")]
    public void Wrong_case_changed_truncated_padded_blank_joined_or_empty_key_is_refused(string presented)
    {
        Assert.False(Authenticator(First).TryAuthenticate(Request(presented), out int index));
        Assert.Equal(-1, index);
    }

    [Fact]
    public void Missing_header_is_refused_when_keys_are_configured() =>
        Assert.False(Authenticator(First).TryAuthenticate(Request(), out _));

    [Fact]
    public void Repeated_header_is_refused_even_when_one_value_matches() =>
        Assert.False(Authenticator(First).TryAuthenticate(Request(First, "other"), out _));

    [Fact]
    public void Partition_is_the_key_index_for_a_valid_key_and_the_address_otherwise()
    {
        ApiKeyAuthenticator authenticator = Authenticator(First, Second);
        Assert.Equal("key:1", ApiRateLimiting.PartitionKey(Request(Second), authenticator));

        DefaultHttpContext wrongKey = Request("wrong-key-0123456789");
        wrongKey.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.9");
        Assert.Equal("address:203.0.113.9", ApiRateLimiting.PartitionKey(wrongKey, authenticator));

        Assert.Equal("address:unknown", ApiRateLimiting.PartitionKey(Request(), authenticator));
    }

    [Fact]
    public void Open_mode_partitions_by_address_even_when_a_header_is_sent()
    {
        DefaultHttpContext context = Request("anything");
        context.Connection.RemoteIpAddress = System.Net.IPAddress.IPv6Loopback;
        Assert.Equal("address:::1", ApiRateLimiting.PartitionKey(context, Authenticator()));
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ApiKeyAuthenticator(null!));
        Assert.Throws<ArgumentNullException>(() => Authenticator(First).TryAuthenticate(null!, out _));
    }
}
