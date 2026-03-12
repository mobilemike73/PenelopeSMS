using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Infrastructure.Twilio;

public sealed record TwilioLookupResult
{
    public bool IsSuccess { get; init; }

    public string? LookupPhoneNumber { get; init; }

    public bool? IsValid { get; init; }

    public IReadOnlyList<string> ValidationErrors { get; init; } = Array.Empty<string>();

    public string? CountryCode { get; init; }

    public string? LineType { get; init; }

    public string? CarrierName { get; init; }

    public string? MobileCountryCode { get; init; }

    public string? MobileNetworkCode { get; init; }

    public string? RawPayloadJson { get; init; }

    public CampaignEligibilityStatus EligibilityStatus { get; init; }

    public EnrichmentFailureStatus FailureStatus { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static TwilioLookupResult Success(
        string? lookupPhoneNumber,
        bool? isValid,
        IReadOnlyList<string> validationErrors,
        string? countryCode,
        string? lineType,
        string? carrierName,
        string? mobileCountryCode,
        string? mobileNetworkCode,
        string? rawPayloadJson)
    {
        return new TwilioLookupResult
        {
            IsSuccess = true,
            LookupPhoneNumber = lookupPhoneNumber,
            IsValid = isValid,
            ValidationErrors = validationErrors,
            CountryCode = countryCode,
            LineType = lineType,
            CarrierName = carrierName,
            MobileCountryCode = mobileCountryCode,
            MobileNetworkCode = mobileNetworkCode,
            RawPayloadJson = rawPayloadJson,
            EligibilityStatus = DeriveEligibility(countryCode, lineType, isValid),
            FailureStatus = EnrichmentFailureStatus.None
        };
    }

    public static TwilioLookupResult Failure(
        EnrichmentFailureStatus failureStatus,
        string errorCode,
        string errorMessage)
    {
        return new TwilioLookupResult
        {
            IsSuccess = false,
            FailureStatus = failureStatus,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            EligibilityStatus = CampaignEligibilityStatus.Ineligible
        };
    }

    public static CampaignEligibilityStatus DeriveEligibility(
        string? countryCode,
        string? lineType,
        bool? isValid)
    {
        if (isValid is not true)
        {
            return CampaignEligibilityStatus.Ineligible;
        }

        if (!string.Equals(countryCode, "US", StringComparison.OrdinalIgnoreCase))
        {
            return CampaignEligibilityStatus.Ineligible;
        }

        if (!string.Equals(lineType, "mobile", StringComparison.OrdinalIgnoreCase))
        {
            return CampaignEligibilityStatus.Ineligible;
        }

        return CampaignEligibilityStatus.Eligible;
    }
}
