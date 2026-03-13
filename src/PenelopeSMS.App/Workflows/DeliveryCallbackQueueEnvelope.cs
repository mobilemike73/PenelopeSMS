namespace PenelopeSMS.App.Workflows;

internal sealed record DeliveryCallbackQueueEnvelope
{
    public string EnvelopeType { get; init; } = string.Empty;

    public string? RejectionReason { get; init; }

    public string? MessageSid { get; init; }

    public string? MessageStatus { get; init; }

    public string? ProviderErrorCode { get; init; }

    public string? ProviderErrorMessage { get; init; }

    public string? ProviderEventRawValue { get; init; }

    public string RawPayloadJson { get; init; } = string.Empty;

    public DateTime ReceivedAtUtc { get; init; }

    public string? SignatureHeader { get; init; }

    public string? ErrorMessage { get; init; }
}
