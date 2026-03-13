using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PenelopeSMS.Infrastructure.Aws;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;

namespace PenelopeSMS.App.Workflows;

public sealed class DeliveryCallbackProcessingWorkflow(
    DeliveryCallbackRepository deliveryCallbackRepository,
    RejectedDeliveryCallbackRepository rejectedDeliveryCallbackRepository) : IDeliveryCallbackProcessingWorkflow
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<DeliveryCallbackProcessingResult> ProcessAsync(
        SqsQueueMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        DeliveryCallbackQueueEnvelope? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<DeliveryCallbackQueueEnvelope>(message.Body, SerializerOptions);
        }
        catch (JsonException exception)
        {
            await StoreRejectedAsync(
                rejectionReason: "malformed_queue_message",
                rawPayloadJson: message.Body,
                signatureHeader: null,
                errorMessage: exception.Message,
                receivedAtUtc: DateTime.UtcNow,
                cancellationToken: cancellationToken);

            return new DeliveryCallbackProcessingResult(
                ShouldDeleteMessage: true,
                Outcome: "rejected_malformed_queue",
                ConsoleMessage: $"Warning: rejected malformed queue payload for message {message.MessageId}: {exception.Message}");
        }

        if (envelope is null)
        {
            await StoreRejectedAsync(
                rejectionReason: "empty_queue_message",
                rawPayloadJson: message.Body,
                signatureHeader: null,
                errorMessage: "Queue message deserialized to null.",
                receivedAtUtc: DateTime.UtcNow,
                cancellationToken: cancellationToken);

            return new DeliveryCallbackProcessingResult(
                ShouldDeleteMessage: true,
                Outcome: "rejected_empty_queue",
                ConsoleMessage: $"Warning: rejected empty callback envelope for queue message {message.MessageId}.");
        }

        if (string.Equals(envelope.EnvelopeType, "rejected", StringComparison.OrdinalIgnoreCase))
        {
            await StoreRejectedAsync(
                rejectionReason: envelope.RejectionReason ?? "rejected_callback",
                rawPayloadJson: envelope.RawPayloadJson,
                signatureHeader: envelope.SignatureHeader,
                errorMessage: envelope.ErrorMessage,
                receivedAtUtc: envelope.ReceivedAtUtc,
                cancellationToken: cancellationToken);

            return new DeliveryCallbackProcessingResult(
                ShouldDeleteMessage: true,
                Outcome: "rejected_callback",
                ConsoleMessage: $"Warning: stored rejected callback ({envelope.RejectionReason ?? "unknown"}) from queue message {message.MessageId}.");
        }

        if (!string.Equals(envelope.EnvelopeType, "delivery", StringComparison.OrdinalIgnoreCase))
        {
            await StoreRejectedAsync(
                rejectionReason: "unsupported_envelope_type",
                rawPayloadJson: envelope.RawPayloadJson,
                signatureHeader: envelope.SignatureHeader,
                errorMessage: $"Unsupported envelope type: {envelope.EnvelopeType}",
                receivedAtUtc: envelope.ReceivedAtUtc,
                cancellationToken: cancellationToken);

            return new DeliveryCallbackProcessingResult(
                ShouldDeleteMessage: true,
                Outcome: "rejected_unsupported_envelope_type",
                ConsoleMessage: $"Warning: rejected unsupported callback envelope type '{envelope.EnvelopeType}' from queue message {message.MessageId}.");
        }

        if (string.IsNullOrWhiteSpace(envelope.MessageSid) || string.IsNullOrWhiteSpace(envelope.MessageStatus))
        {
            await StoreRejectedAsync(
                rejectionReason: "malformed_delivery_callback",
                rawPayloadJson: envelope.RawPayloadJson,
                signatureHeader: envelope.SignatureHeader,
                errorMessage: "Delivery callback is missing MessageSid or MessageStatus.",
                receivedAtUtc: envelope.ReceivedAtUtc,
                cancellationToken: cancellationToken);

            return new DeliveryCallbackProcessingResult(
                ShouldDeleteMessage: true,
                Outcome: "rejected_missing_required_fields",
                ConsoleMessage: $"Warning: stored malformed delivery callback from queue message {message.MessageId}.");
        }

        if (!IsSupportedDeliveryStatus(envelope.MessageStatus))
        {
            await StoreRejectedAsync(
                rejectionReason: "unsupported_delivery_status",
                rawPayloadJson: envelope.RawPayloadJson,
                signatureHeader: envelope.SignatureHeader,
                errorMessage: $"Unsupported delivery status: {envelope.MessageStatus}",
                receivedAtUtc: envelope.ReceivedAtUtc,
                cancellationToken: cancellationToken);

            return new DeliveryCallbackProcessingResult(
                ShouldDeleteMessage: true,
                Outcome: "rejected_unsupported_status",
                ConsoleMessage: $"Warning: rejected unsupported delivery status '{envelope.MessageStatus}' from queue message {message.MessageId}.");
        }

        var applyResult = await deliveryCallbackRepository.ApplyAsync(
            new DeliveryCallbackEnvelopeMessage(
                MessageSid: envelope.MessageSid,
                MessageStatus: envelope.MessageStatus,
                ProviderErrorCode: envelope.ProviderErrorCode,
                ProviderErrorMessage: envelope.ProviderErrorMessage,
                ProviderEventRawValue: envelope.ProviderEventRawValue,
                RawPayloadJson: envelope.RawPayloadJson,
                ReceivedAtUtc: envelope.ReceivedAtUtc),
            cancellationToken);

        return new DeliveryCallbackProcessingResult(
            ShouldDeleteMessage: applyResult.ShouldDeleteMessage,
            Outcome: applyResult.Outcome,
            ConsoleMessage: BuildConsoleMessage(message.MessageId, envelope, applyResult),
            MessageStatus: envelope.MessageStatus);
    }

    private static string BuildConsoleMessage(
        string messageId,
        DeliveryCallbackQueueEnvelope envelope,
        DeliveryCallbackApplyResult applyResult)
    {
        return $"Processed callback queue message {messageId}: {applyResult.Outcome} | SID: {envelope.MessageSid ?? "missing"} | Status: {envelope.MessageStatus ?? "missing"}";
    }

    private async Task StoreRejectedAsync(
        string rejectionReason,
        string rawPayloadJson,
        string? signatureHeader,
        string? errorMessage,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken)
    {
        await rejectedDeliveryCallbackRepository.StoreAsync(
            new RejectedDeliveryCallbackRecord(
                RejectionReason: rejectionReason,
                CallbackFingerprint: ComputeFingerprint(rawPayloadJson),
                RawPayloadJson: rawPayloadJson,
                SignatureHeader: signatureHeader,
                ErrorMessage: errorMessage,
                ReceivedAtUtc: receivedAtUtc),
            cancellationToken);
    }

    private static string ComputeFingerprint(string rawPayloadJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawPayloadJson));
        return Convert.ToHexString(bytes);
    }

    private static bool IsSupportedDeliveryStatus(string messageStatus)
    {
        return messageStatus.Trim().ToLowerInvariant() switch
        {
            "queued" => true,
            "sent" => true,
            "delivered" => true,
            "undelivered" => true,
            "failed" => true,
            _ => false
        };
    }
}
