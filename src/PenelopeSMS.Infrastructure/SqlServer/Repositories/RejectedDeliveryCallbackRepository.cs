using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Entities;

namespace PenelopeSMS.Infrastructure.SqlServer.Repositories;

public sealed class RejectedDeliveryCallbackRepository(PenelopeSmsDbContext dbContext)
{
    public async Task StoreAsync(
        RejectedDeliveryCallbackRecord rejectedCallback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rejectedCallback);

        var existing = await dbContext.RejectedDeliveryCallbacks
            .SingleOrDefaultAsync(
                callback => callback.CallbackFingerprint == rejectedCallback.CallbackFingerprint,
                cancellationToken);

        if (existing is not null)
        {
            existing.LastSeenAtUtc = rejectedCallback.ReceivedAtUtc > existing.LastSeenAtUtc
                ? rejectedCallback.ReceivedAtUtc
                : existing.LastSeenAtUtc;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        dbContext.RejectedDeliveryCallbacks.Add(new RejectedDeliveryCallback
        {
            RejectionReason = rejectedCallback.RejectionReason,
            CallbackFingerprint = rejectedCallback.CallbackFingerprint,
            RawPayloadJson = rejectedCallback.RawPayloadJson,
            SignatureHeader = rejectedCallback.SignatureHeader,
            ErrorMessage = rejectedCallback.ErrorMessage,
            ReceivedAtUtc = rejectedCallback.ReceivedAtUtc,
            FirstSeenAtUtc = rejectedCallback.ReceivedAtUtc,
            LastSeenAtUtc = rejectedCallback.ReceivedAtUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed record RejectedDeliveryCallbackRecord(
    string RejectionReason,
    string CallbackFingerprint,
    string RawPayloadJson,
    string? SignatureHeader,
    string? ErrorMessage,
    DateTime ReceivedAtUtc);
