using DocumentRedaction.Core.Detectors.Validation;

namespace DocumentRedaction.Core.Tests.Detectors;

public class ChecksumsTests
{
    [Theory]
    [InlineData("4111111111111111", true)]
    [InlineData("5500000000000004", true)]
    [InlineData("378282246310005", true)]
    [InlineData("4111111111111112", false)]
    [InlineData("0", true)]
    [InlineData("", false)]
    [InlineData("4111a11111111111", false)]
    public void Luhn(string digits, bool expected) => Assert.Equal(expected, Checksums.IsValidLuhn(digits));

    [Theory]
    [InlineData("1234567893", true)]
    [InlineData("1234567890", false)]
    [InlineData("123456789", false)]
    [InlineData("12345678931", false)]
    public void Npi(string digits, bool expected) => Assert.Equal(expected, Checksums.IsValidNpi(digits));

    [Theory]
    [InlineData("021000021", true)]
    [InlineData("011000015", true)]
    [InlineData("122105155", true)]
    [InlineData("021000022", false)]
    [InlineData("131000021", false)] // checksum could pass but prefix 13 is not assigned
    [InlineData("02100002", false)]
    [InlineData("02100002a", false)]
    public void AbaRouting(string digits, bool expected) => Assert.Equal(expected, Checksums.IsValidAbaRoutingNumber(digits));

    [Theory]
    [InlineData("GB82WEST12345698765432", true)]
    [InlineData("DE89370400440532013000", true)]
    [InlineData("FR1420041010050500013M02606", true)]
    [InlineData("GB82WEST12345698765433", false)]
    [InlineData("GB82WEST1234", false)]
    [InlineData("GB82 WEST 1234 5698 7654 32", false)] // caller strips spaces first
    public void Iban(string iban, bool expected) => Assert.Equal(expected, Checksums.IsValidIban(iban));

    [Theory]
    [InlineData("AB1234563", true)]
    [InlineData("FA9876543", false)]
    [InlineData("AB1234567", false)]
    [InlineData("AB123456", false)]
    [InlineData("AB12345X3", false)]
    public void Dea(string value, bool expected) => Assert.Equal(expected, Checksums.IsValidDeaNumber(value));
}
