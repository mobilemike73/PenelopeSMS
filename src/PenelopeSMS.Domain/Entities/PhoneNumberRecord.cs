using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Domain.Entities;

public sealed class PhoneNumberRecord
{
    public int Id { get; set; }

    public string CanonicalPhoneNumber { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastImportedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastEnrichmentAttemptedAtUtc { get; set; }

    public DateTime? LastEnrichedAtUtc { get; set; }

    public string? TwilioCountryCode { get; set; }

    public string? TwilioLineType { get; set; }

    public string? TwilioCarrierName { get; set; }

    public string? TwilioMobileCountryCode { get; set; }

    public string? TwilioMobileNetworkCode { get; set; }

    public string? TwilioLookupPayloadJson { get; set; }

    public CampaignEligibilityStatus CampaignEligibilityStatus { get; set; } =
        CampaignEligibilityStatus.Pending;

    public DateTime? EligibilityEvaluatedAtUtc { get; set; }

    public EnrichmentFailureStatus EnrichmentFailureStatus { get; set; } =
        EnrichmentFailureStatus.None;

    public DateTime? LastEnrichmentFailedAtUtc { get; set; }

    public string? LastEnrichmentErrorCode { get; set; }

    public string? LastEnrichmentErrorMessage { get; set; }

    public ICollection<CustomerPhoneLink> CustomerPhoneLinks { get; } = new List<CustomerPhoneLink>();
}
