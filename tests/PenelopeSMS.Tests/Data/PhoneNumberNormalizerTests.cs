using PenelopeSMS.Domain.Services;

namespace PenelopeSMS.Tests.Data;

public sealed class PhoneNumberNormalizerTests
{
    private readonly PhoneNumberNormalizer normalizer = new();

    [Fact]
    public void NormalizeReturnsCanonicalE164NumberAndPreservesRawInput()
    {
        var normalizedPhoneNumber = normalizer.Normalize("650-253-0000", "us");

        Assert.Equal("650-253-0000", normalizedPhoneNumber.RawInput);
        Assert.Equal("+16502530000", normalizedPhoneNumber.CanonicalPhoneNumber);
        Assert.Equal("US", normalizedPhoneNumber.RegionCode);
    }

    [Fact]
    public void NormalizeRejectsInvalidNumbers()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => normalizer.Normalize("123", "US"));

        Assert.Contains("not valid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
