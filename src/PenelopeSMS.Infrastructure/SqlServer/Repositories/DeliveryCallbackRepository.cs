using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Infrastructure.SqlServer.Repositories;

public sealed class DeliveryCallbackRepository(PenelopeSmsDbContext dbContext)
{
    public async Task<DeliveryCallbackApplyResult> ApplyAsync(
        DeliveryCallbackEnvelopeMessage envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (string.IsNullOrWhiteSpace(envelope.MessageSid))
        {
            throw new InvalidOperationException("Delivery callbacks require a Twilio Message SID.");
        }

        var fingerprint = ComputeFingerprint(envelope);
        var recipient = await dbContext.CampaignRecipients
            .Include(candidate => candidate.StatusHistory)
            .SingleOrDefaultAsync(
                candidate => candidate.TwilioMessageSid == envelope.MessageSid,
                cancellationToken);

        if (recipient is null)
        {
            await StoreUnmatchedAsync(envelope, fingerprint, cancellationToken);
            return DeliveryCallbackApplyResult.Unmatched();
        }

        var existingHistory = recipient.StatusHistory
            .SingleOrDefault(history => history.CallbackFingerprint == fingerprint);

        if (existingHistory is not null)
        {
            existingHistory.LastSeenAtUtc = MaxTimestamp(existingHistory.LastSeenAtUtc, envelope.ReceivedAtUtc);
            recipient.LastDeliveryCallbackReceivedAtUtc = MaxTimestamp(recipient.LastDeliveryCallbackReceivedAtUtc, envelope.ReceivedAtUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            return DeliveryCallbackApplyResult.Duplicate();
        }

        var incomingStatus = MapStatus(envelope.MessageStatus);
        var (providerEventAtUtc, timeSource, rawValue) = ResolveProviderEventTime(envelope);

        if (recipient.CurrentStatusAtUtc.HasValue
            && providerEventAtUtc < recipient.CurrentStatusAtUtc.Value)
        {
            recipient.LastDeliveryCallbackReceivedAtUtc = MaxTimestamp(recipient.LastDeliveryCallbackReceivedAtUtc, envelope.ReceivedAtUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            return DeliveryCallbackApplyResult.OlderDiscarded();
        }

        if (ShouldDiscardForStatusRegression(recipient.Status, incomingStatus))
        {
            recipient.LastDeliveryCallbackReceivedAtUtc = MaxTimestamp(recipient.LastDeliveryCallbackReceivedAtUtc, envelope.ReceivedAtUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            return DeliveryCallbackApplyResult.OlderDiscarded();
        }

        recipient.Status = incomingStatus;
        recipient.CurrentStatusAtUtc = providerEventAtUtc;
        recipient.CurrentStatusTimeSource = timeSource;
        recipient.CurrentStatusRawValue = rawValue;
        recipient.LastDeliveryCallbackReceivedAtUtc = envelope.ReceivedAtUtc;
        recipient.DeliveryErrorCode = envelope.ProviderErrorCode;
        recipient.DeliveryErrorMessage = envelope.ProviderErrorMessage;

        recipient.StatusHistory.Add(new CampaignRecipientStatusHistory
        {
            Status = incomingStatus,
            ProviderEventAtUtc = providerEventAtUtc,
            EventTimeSource = timeSource,
            ProviderEventRawValue = rawValue,
            ProviderErrorCode = envelope.ProviderErrorCode,
            ProviderErrorMessage = envelope.ProviderErrorMessage,
            RawPayloadJson = envelope.RawPayloadJson,
            CallbackFingerprint = fingerprint,
            ReceivedAtUtc = envelope.ReceivedAtUtc,
            FirstSeenAtUtc = envelope.ReceivedAtUtc,
            LastSeenAtUtc = envelope.ReceivedAtUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return DeliveryCallbackApplyResult.Applied(incomingStatus, providerEventAtUtc);
    }

    private async Task StoreUnmatchedAsync(
        DeliveryCallbackEnvelopeMessage envelope,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.UnmatchedDeliveryCallbacks
            .SingleOrDefaultAsync(
                callback => callback.CallbackFingerprint == fingerprint,
                cancellationToken);

        if (existing is not null)
        {
            existing.LastSeenAtUtc = MaxTimestamp(existing.LastSeenAtUtc, envelope.ReceivedAtUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        dbContext.UnmatchedDeliveryCallbacks.Add(new UnmatchedDeliveryCallback
        {
            TwilioMessageSid = envelope.MessageSid,
            MessageStatus = envelope.MessageStatus,
            CallbackFingerprint = fingerprint,
            RawPayloadJson = envelope.RawPayloadJson,
            ProviderErrorCode = envelope.ProviderErrorCode,
            ProviderErrorMessage = envelope.ProviderErrorMessage,
            ProviderEventRawValue = envelope.ProviderEventRawValue,
            ReceivedAtUtc = envelope.ReceivedAtUtc,
            FirstSeenAtUtc = envelope.ReceivedAtUtc,
            LastSeenAtUtc = envelope.ReceivedAtUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CampaignRecipientStatus MapStatus(string? messageStatus)
    {
        return messageStatus?.Trim().ToLowerInvariant() switch
        {
            "queued" => CampaignRecipientStatus.Queued,
            "sent" => CampaignRecipientStatus.Sent,
            "delivered" => CampaignRecipientStatus.Delivered,
            "undelivered" => CampaignRecipientStatus.Undelivered,
            "failed" => CampaignRecipientStatus.Failed,
            _ => throw new InvalidOperationException($"Unsupported delivery status: {messageStatus ?? "<null>"}")
        };
    }

    private static (DateTime ProviderEventAtUtc, DeliveryEventTimeSource Source, string? RawValue)
        ResolveProviderEventTime(DeliveryCallbackEnvelopeMessage envelope)
    {
        if (!string.IsNullOrWhiteSpace(envelope.ProviderEventRawValue)
            && DateTime.TryParseExact(
                envelope.ProviderEventRawValue,
                "yyMMddHHmm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedEventAtUtc))
        {
            return (parsedEventAtUtc, DeliveryEventTimeSource.RawDlrDoneDate, envelope.ProviderEventRawValue);
        }

        return (envelope.ReceivedAtUtc, DeliveryEventTimeSource.CallbackReceivedAt, envelope.ProviderEventRawValue);
    }

    private static string ComputeFingerprint(DeliveryCallbackEnvelopeMessage envelope)
    {
        var payload = string.Join('|',
            envelope.MessageSid ?? string.Empty,
            envelope.MessageStatus ?? string.Empty,
            envelope.ProviderErrorCode ?? string.Empty,
            envelope.ProviderErrorMessage ?? string.Empty,
            envelope.ProviderEventRawValue ?? string.Empty,
            envelope.RawPayloadJson);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private static bool IsTerminal(CampaignRecipientStatus status)
    {
        return status is CampaignRecipientStatus.Delivered
            or CampaignRecipientStatus.Undelivered
            or CampaignRecipientStatus.Failed;
    }

    private static bool ShouldDiscardForStatusRegression(
        CampaignRecipientStatus currentStatus,
        CampaignRecipientStatus incomingStatus)
    {
        if (currentStatus == incomingStatus)
        {
            return false;
        }

        if (IsTerminal(currentStatus))
        {
            return true;
        }

        return GetStatusProgressRank(incomingStatus) < GetStatusProgressRank(currentStatus);
    }

    private static int GetStatusProgressRank(CampaignRecipientStatus status)
    {
        return status switch
        {
            CampaignRecipientStatus.Pending => 0,
            CampaignRecipientStatus.Submitted => 1,
            CampaignRecipientStatus.Queued => 2,
            CampaignRecipientStatus.Sent => 3,
            CampaignRecipientStatus.Delivered => 4,
            CampaignRecipientStatus.Undelivered => 4,
            CampaignRecipientStatus.Failed => 4,
            _ => throw new InvalidOperationException($"Unsupported campaign recipient status: {status}")
        };
    }

    private static DateTime MaxTimestamp(DateTime existingTimestamp, DateTime candidateTimestamp)
    {
        return candidateTimestamp > existingTimestamp
            ? candidateTimestamp
            : existingTimestamp;
    }

    private static DateTime? MaxTimestamp(DateTime? existingTimestamp, DateTime candidateTimestamp)
    {
        if (!existingTimestamp.HasValue)
        {
            return candidateTimestamp;
        }

        return candidateTimestamp > existingTimestamp.Value
            ? candidateTimestamp
            : existingTimestamp.Value;
    }
}

public sealed record DeliveryCallbackEnvelopeMessage(
    string? MessageSid,
    string? MessageStatus,
    string? ProviderErrorCode,
    string? ProviderErrorMessage,
    string? ProviderEventRawValue,
    string RawPayloadJson,
    DateTime ReceivedAtUtc);

public sealed record DeliveryCallbackApplyResult(
    string Outcome,
    bool ShouldDeleteMessage,
    CampaignRecipientStatus? AppliedStatus = null,
    DateTime? AppliedEventAtUtc = null)
{
    public static DeliveryCallbackApplyResult Applied(
        CampaignRecipientStatus appliedStatus,
        DateTime appliedEventAtUtc)
        => new("applied", true, appliedStatus, appliedEventAtUtc);

    public static DeliveryCallbackApplyResult Duplicate()
        => new("duplicate", true);

    public static DeliveryCallbackApplyResult OlderDiscarded()
        => new("older_discarded", true);

    public static DeliveryCallbackApplyResult Unmatched()
        => new("unmatched", true);
}
