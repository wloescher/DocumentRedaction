namespace DocumentRedaction.Core.Detectors.Validation;

/// <summary>Checksum algorithms used to confirm that a numeric match is a real identifier.</summary>
public static class Checksums
{
    /// <summary>Luhn (mod 10) check over a digit-only string.</summary>
    public static bool IsValidLuhn(string digits)
    {
        if (string.IsNullOrEmpty(digits) || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        int sum = 0;
        bool doubleIt = false;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int digit = digits[i] - '0';
            if (doubleIt)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubleIt = !doubleIt;
        }

        return sum % 10 == 0;
    }

    /// <summary>
    /// NPI numbers are Luhn-valid once prefixed with the health-industry identifier "80840".
    /// </summary>
    public static bool IsValidNpi(string digits) =>
        digits.Length == 10 && IsValidLuhn("80840" + digits);

    /// <summary>ABA routing transit number: weighted mod-10 plus a valid Federal Reserve prefix.</summary>
    public static bool IsValidAbaRoutingNumber(string digits)
    {
        if (digits.Length != 9 || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        int prefix = (digits[0] - '0') * 10 + (digits[1] - '0');
        bool prefixOk = prefix is (>= 0 and <= 12) or (>= 21 and <= 32) or (>= 61 and <= 72) or 80;
        if (!prefixOk)
        {
            return false;
        }

        int D(int i) => digits[i] - '0';
        int sum = 3 * (D(0) + D(3) + D(6)) + 7 * (D(1) + D(4) + D(7)) + (D(2) + D(5) + D(8));
        return sum % 10 == 0;
    }

    /// <summary>IBAN mod-97 check (ISO 13616). Accepts letters and digits only; strip spaces first.</summary>
    public static bool IsValidIban(string iban)
    {
        if (iban.Length is < 15 or > 34 || !iban.All(char.IsAsciiLetterOrDigit))
        {
            return false;
        }

        string rearranged = string.Concat(iban.AsSpan(4), iban.AsSpan(0, 4));
        int remainder = 0;
        foreach (char c in rearranged)
        {
            int value = char.IsAsciiDigit(c) ? c - '0' : char.ToUpperInvariant(c) - 'A' + 10;
            // Two-digit letter values need two mod steps to stay within int range.
            remainder = value < 10
                ? (remainder * 10 + value) % 97
                : (remainder * 100 + value) % 97;
        }

        return remainder == 1;
    }

    /// <summary>
    /// DEA registration number: two letters then seven digits, where the seventh digit equals the
    /// last digit of (d1 + d3 + d5) + 2 * (d2 + d4 + d6).
    /// </summary>
    public static bool IsValidDeaNumber(string value)
    {
        if (value.Length != 9 || !value[2..].All(char.IsAsciiDigit))
        {
            return false;
        }

        int D(int i) => value[i + 2] - '0';
        int check = (D(0) + D(2) + D(4)) + 2 * (D(1) + D(3) + D(5));
        return check % 10 == D(6);
    }
}
