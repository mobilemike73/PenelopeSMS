namespace PenelopeSMS.Domain.Services;

public interface IPhoneNumberNormalizer
{
    PhoneNumberNormalizationResult Normalize(string rawPhoneNumber, string defaultRegion);

    bool TryNormalize(
        string rawPhoneNumber,
        string defaultRegion,
        out PhoneNumberNormalizationResult? normalizedPhoneNumber,
        out string? errorMessage);
}

public sealed record PhoneNumberNormalizationResult(
    string RawInput,
    string CanonicalPhoneNumber,
    string RegionCode);
