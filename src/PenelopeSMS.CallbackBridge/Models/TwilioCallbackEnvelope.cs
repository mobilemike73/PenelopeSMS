namespace PenelopeSMS.CallbackBridge.Models;

public sealed record TwilioCallbackEnvelope
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

    public static TwilioCallbackEnvelope CreateDelivery(
        string messageSid,
        string? messageStatus,
        string? providerErrorCode,
        string? providerErrorMessage,
        string? providerEventRawValue,
        string rawPayloadJson,
        DateTime receivedAtUtc,
        string? signatureHeader)
    {
        return new TwilioCallbackEnvelope
        {
            EnvelopeType = "delivery",
            MessageSid = messageSid,
            MessageStatus = messageStatus,
            ProviderErrorCode = providerErrorCode,
            ProviderErrorMessage = providerErrorMessage,
            ProviderEventRawValue = providerEventRawValue,
            RawPayloadJson = rawPayloadJson,
            ReceivedAtUtc = receivedAtUtc,
            SignatureHeader = signatureHeader
        };
    }

    public static TwilioCallbackEnvelope CreateRejected(
        string rejectionReason,
        string rawPayloadJson,
        DateTime receivedAtUtc,
        string? signatureHeader,
        string? errorMessage)
    {
        return new TwilioCallbackEnvelope
        {
            EnvelopeType = "rejected",
            RejectionReason = rejectionReason,
            RawPayloadJson = rawPayloadJson,
            ReceivedAtUtc = receivedAtUtc,
            SignatureHeader = signatureHeader,
            ErrorMessage = errorMessage
        };
    }
}
