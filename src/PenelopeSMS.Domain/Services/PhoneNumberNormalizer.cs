using PhoneNumbers;

namespace PenelopeSMS.Domain.Services;

public sealed class PhoneNumberNormalizer : IPhoneNumberNormalizer
{
    private readonly PhoneNumberUtil phoneNumberUtil = PhoneNumberUtil.GetInstance();

    public PhoneNumberNormalizationResult Normalize(string rawPhoneNumber, string defaultRegion)
    {
        if (string.IsNullOrWhiteSpace(rawPhoneNumber))
        {
            throw new ArgumentException("A raw phone number is required.", nameof(rawPhoneNumber));
        }

        if (string.IsNullOrWhiteSpace(defaultRegion))
        {
            throw new ArgumentException("A default region is required.", nameof(defaultRegion));
        }

        var normalizedRegion = defaultRegion.Trim().ToUpperInvariant();

        PhoneNumber parsedNumber;

        try
        {
            parsedNumber = phoneNumberUtil.Parse(rawPhoneNumber, normalizedRegion);
        }
        catch (NumberParseException ex)
        {
            throw new InvalidOperationException(
                $"The phone number '{rawPhoneNumber}' could not be parsed for region '{normalizedRegion}'.",
                ex);
        }

        if (!phoneNumberUtil.IsValidNumber(parsedNumber))
        {
            throw new InvalidOperationException(
                $"The phone number '{rawPhoneNumber}' is not valid for region '{normalizedRegion}'.");
        }

        var canonicalPhoneNumber = phoneNumberUtil.Format(parsedNumber, PhoneNumberFormat.E164);
        var regionCode = phoneNumberUtil.GetRegionCodeForNumber(parsedNumber) ?? normalizedRegion;

        return new PhoneNumberNormalizationResult(rawPhoneNumber, canonicalPhoneNumber, regionCode);
    }
}
