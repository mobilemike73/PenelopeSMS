using PhoneNumbers;

namespace PenelopeSMS.Domain.Services;

public sealed class PhoneNumberNormalizer : IPhoneNumberNormalizer
{
    private readonly PhoneNumberUtil phoneNumberUtil = PhoneNumberUtil.GetInstance();

    public PhoneNumberNormalizationResult Normalize(string rawPhoneNumber, string defaultRegion)
    {
        if (TryNormalize(rawPhoneNumber, defaultRegion, out var normalizedPhoneNumber, out var errorMessage))
        {
            return normalizedPhoneNumber!;
        }

        throw new InvalidOperationException(errorMessage);
    }

    public bool TryNormalize(
        string rawPhoneNumber,
        string defaultRegion,
        out PhoneNumberNormalizationResult? normalizedPhoneNumber,
        out string? errorMessage)
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
        catch (NumberParseException)
        {
            normalizedPhoneNumber = null;
            errorMessage =
                $"The phone number '{rawPhoneNumber}' could not be parsed for region '{normalizedRegion}'.";
            return false;
        }

        if (!phoneNumberUtil.IsValidNumber(parsedNumber))
        {
            normalizedPhoneNumber = null;
            errorMessage =
                $"The phone number '{rawPhoneNumber}' is not valid for region '{normalizedRegion}'.";
            return false;
        }

        var canonicalPhoneNumber = phoneNumberUtil.Format(parsedNumber, PhoneNumberFormat.E164);
        var regionCode = phoneNumberUtil.GetRegionCodeForNumber(parsedNumber) ?? normalizedRegion;

        normalizedPhoneNumber = new PhoneNumberNormalizationResult(rawPhoneNumber, canonicalPhoneNumber, regionCode);
        errorMessage = null;
        return true;
    }
}
