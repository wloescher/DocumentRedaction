using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace DocumentRedaction.Web.Api;

/// <summary>
/// Checks the <c>X-Api-Key</c> header against the configured keys. With no keys configured every
/// request is accepted (open mode). Comparison is constant-time so response timing does not leak
/// how much of a guessed key was right, and key values are never logged or echoed.
/// </summary>
public sealed class ApiKeyAuthenticator
{
    public const string HeaderName = "X-Api-Key";

    private readonly byte[][] _keys;

    public ApiKeyAuthenticator(IOptions<RedactionSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _keys = settings.Value.ApiKeys.Select(Encoding.UTF8.GetBytes).ToArray();
    }

    /// <summary>True when no key is configured and the header is ignored.</summary>
    public bool IsOpen => _keys.Length == 0;

    /// <summary>
    /// <paramref name="keyIndex"/> is the position of the matching configured key, or -1 when the
    /// API is open or the request was refused. Callers partition rate limits by that index so a
    /// key's identity is tracked without holding the secret.
    /// </summary>
    public bool TryAuthenticate(HttpContext httpContext, out int keyIndex)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        keyIndex = -1;
        if (IsOpen)
        {
            return true;
        }

        // Exactly one header value; repeated or comma-joined headers are refused rather than guessed at.
        StringValues values = httpContext.Request.Headers[HeaderName];
        if (values.Count != 1 || values[0] is not { Length: > 0 } presented)
        {
            return false;
        }

        byte[] candidate = Encoding.UTF8.GetBytes(presented);
        for (int i = 0; i < _keys.Length; i++)
        {
            if (CryptographicOperations.FixedTimeEquals(candidate, _keys[i]))
            {
                keyIndex = i;
                return true;
            }
        }

        return false;
    }
}
