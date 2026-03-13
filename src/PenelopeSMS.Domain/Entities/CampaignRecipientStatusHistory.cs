using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Domain.Entities;

public sealed class CampaignRecipientStatusHistory
{
    public int Id { get; set; }

    public int CampaignRecipientId { get; set; }

    public CampaignRecipient CampaignRecipient { get; set; } = null!;

    public CampaignRecipientStatus Status { get; set; }

    public DateTime ProviderEventAtUtc { get; set; }

    public DeliveryEventTimeSource EventTimeSource { get; set; } = DeliveryEventTimeSource.Unknown;

    public string? ProviderEventRawValue { get; set; }

    public string? ProviderErrorCode { get; set; }

    public string? ProviderErrorMessage { get; set; }

    public string RawPayloadJson { get; set; } = string.Empty;

    public string CallbackFingerprint { get; set; } = string.Empty;

    public DateTime ReceivedAtUtc { get; set; }

    public DateTime FirstSeenAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
}
