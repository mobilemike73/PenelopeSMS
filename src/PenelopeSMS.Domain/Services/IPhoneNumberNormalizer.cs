namespace PenelopeSMS.Domain.Services;

public interface IPhoneNumberNormalizer
{
    PhoneNumberNormalizationResult Normalize(string rawPhoneNumber, string defaultRegion);
}

public sealed record PhoneNumberNormalizationResult(
    string RawInput,
    string CanonicalPhoneNumber,
    string RegionCode);
