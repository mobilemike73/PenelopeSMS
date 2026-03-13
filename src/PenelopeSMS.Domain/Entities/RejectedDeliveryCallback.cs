namespace PenelopeSMS.Domain.Entities;

public sealed class RejectedDeliveryCallback
{
    public int Id { get; set; }

    public string RejectionReason { get; set; } = string.Empty;

    public string CallbackFingerprint { get; set; } = string.Empty;

    public string RawPayloadJson { get; set; } = string.Empty;

    public string? SignatureHeader { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime ReceivedAtUtc { get; set; }

    public DateTime FirstSeenAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
}
