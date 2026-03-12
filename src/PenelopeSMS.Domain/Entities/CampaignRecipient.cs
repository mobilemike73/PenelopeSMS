using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Domain.Entities;

public sealed class CampaignRecipient
{
    public int Id { get; set; }

    public int CampaignId { get; set; }

    public Campaign Campaign { get; set; } = null!;

    public int PhoneNumberRecordId { get; set; }

    public PhoneNumberRecord PhoneNumberRecord { get; set; } = null!;

    public CampaignRecipientStatus Status { get; set; } = CampaignRecipientStatus.Pending;

    public string? TwilioMessageSid { get; set; }

    public string? InitialTwilioStatus { get; set; }

    public string? ProviderErrorCode { get; set; }

    public string? ProviderErrorMessage { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastAttemptedAtUtc { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }
}
