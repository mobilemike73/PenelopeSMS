namespace PenelopeSMS.Domain.Entities;

public sealed class UnmatchedDeliveryCallback
{
    public int Id { get; set; }

    public string? TwilioMessageSid { get; set; }

    public string? MessageStatus { get; set; }

    public string CallbackFingerprint { get; set; } = string.Empty;

    public string RawPayloadJson { get; set; } = string.Empty;

    public string? ProviderErrorCode { get; set; }

    public string? ProviderErrorMessage { get; set; }

    public string? ProviderEventRawValue { get; set; }

    public DateTime ReceivedAtUtc { get; set; }

    public DateTime FirstSeenAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
}
