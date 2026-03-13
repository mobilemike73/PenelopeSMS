using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Infrastructure.SqlServer.Queries;

public sealed class PendingDeliveryCallbackBatchQuery(PenelopeSmsDbContext dbContext)
{
    public Task<DeliveryCallbackRecipientRecord?> FindRecipientByMessageSidAsync(
        string twilioMessageSid,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(twilioMessageSid);

        return dbContext.CampaignRecipients
            .AsNoTracking()
            .Where(recipient => recipient.TwilioMessageSid == twilioMessageSid)
            .Select(recipient => new DeliveryCallbackRecipientRecord(
                recipient.Id,
                recipient.Status,
                recipient.CurrentStatusAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

public sealed record DeliveryCallbackRecipientRecord(
    int CampaignRecipientId,
    CampaignRecipientStatus CurrentStatus,
    DateTime? CurrentStatusAtUtc);
