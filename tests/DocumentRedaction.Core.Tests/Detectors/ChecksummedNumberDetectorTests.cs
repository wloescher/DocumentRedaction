using DocumentRedaction.Core.Detectors.Builtin;

namespace DocumentRedaction.Core.Tests.Detectors;

/// <summary>Detectors whose pattern is loose but whose checksum validation is strict.</summary>
public class ChecksummedNumberDetectorTests
{
    [Theory]
    [InlineData("card 4111111111111111 ok", "4111111111111111")]
    [InlineData("card 4111 1111 1111 1111 ok", "4111 1111 1111 1111")]
    [InlineData("card 4111-1111-1111-1111 ok", "4111-1111-1111-1111")]
    [InlineData("card 3782 822463 10005 ok", "3782 822463 10005")]
    [InlineData("card 378282246310005 ok", "378282246310005")]
    [InlineData("card 5500 0000 0000 0004 ok", "5500 0000 0000 0004")]
    [InlineData("card 6011111111111117 ok", "6011111111111117")]
    public void CreditCard_finds(string text, string expected) => DetectorAssert.Finds(new CreditCardNumberDetector(), text, expected);

    [Theory]
    [InlineData("4111111111111112")]
    [InlineData("4111 1111-1111 1111")] // mixed separators
    [InlineData("411111111111")] // 12 digits
    [InlineData("1234567890123")] // 13 digits, invalid Luhn
    [InlineData("14111111111111111")] // leading digit glued on
    public void CreditCard_rejects(string text) => DetectorAssert.FindsNothing(new CreditCardNumberDetector(), text);

    [Theory]
    [InlineData("NPI 1234567893", "1234567893")]
    [InlineData("provider 1234567893.", "1234567893")]
    public void Npi_finds(string text, string expected) => DetectorAssert.Finds(new NationalProviderIdDetector(), text, expected);

    [Theory]
    [InlineData("1234567890")]
    [InlineData("11234567893")]
    [InlineData("1234567893-1")]
    public void Npi_rejects(string text) => DetectorAssert.FindsNothing(new NationalProviderIdDetector(), text);

    [Theory]
    [InlineData("routing 021000021 x", "021000021")]
    [InlineData("routing 122105155 x", "122105155")]
    public void Routing_finds(string text, string expected) => DetectorAssert.Finds(new BankRoutingNumberDetector(), text, expected);

    [Theory]
    [InlineData("021000022")]
    [InlineData("123456789")]
    [InlineData("0210000210")]
    public void Routing_rejects(string text) => DetectorAssert.FindsNothing(new BankRoutingNumberDetector(), text);

    [Theory]
    [InlineData("IBAN GB82 WEST 1234 5698 7654 32 ok", "GB82 WEST 1234 5698 7654 32")]
    [InlineData("IBAN GB82WEST12345698765432 ok", "GB82WEST12345698765432")]
    [InlineData("IBAN DE89 3704 0044 0532 0130 00 ok", "DE89 3704 0044 0532 0130 00")]
    [InlineData("IBAN FR14 2004 1010 0505 0001 3M02 606 ok", "FR14 2004 1010 0505 0001 3M02 606")]
    [InlineData("IBAN GB82 WEST 1234 5698 7654 32 USD", "GB82 WEST 1234 5698 7654 32")]
    [InlineData("IBAN GB82WEST12345698765432 EUR", "GB82WEST12345698765432")]
    public void Iban_finds(string text, string expected) => DetectorAssert.Finds(new IbanDetector(), text, expected);

    [Theory]
    [InlineData("GB82 WEST 1234 5698 7654 33")]
    [InlineData("GB82WEST1234")]
    [InlineData("gb82west12345698765432")]
    [InlineData("ZZ82WEST12345698765432")] // unknown country
    [InlineData("GB82WEST123456987654321")] // one digit too long, glued
    public void Iban_rejects(string text) => DetectorAssert.FindsNothing(new IbanDetector(), text);

    [Theory]
    [InlineData("DEA AB1234563 ok", "AB1234563")]
    [InlineData("DEA: FA1234563", "FA1234563")]
    [InlineData("M91234563", "M91234563")]
    public void Dea_finds(string text, string expected) => DetectorAssert.Finds(new DeaNumberDetector(), text, expected);

    [Theory]
    [InlineData("AB1234567")]
    [InlineData("ZB1234563")] // Z is not a registrant type
    [InlineData("ab1234563")]
    [InlineData("AB12345631")]
    public void Dea_rejects(string text) => DetectorAssert.FindsNothing(new DeaNumberDetector(), text);
}
