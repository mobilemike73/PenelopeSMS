using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Infrastructure.SqlServer.Queries;

public sealed class CampaignSendBatchQuery(PenelopeSmsDbContext dbContext)
{
    public Task<List<CampaignSendBatchRecord>> ListPendingBatchAsync(
        int campaignId,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(campaignId, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        return dbContext.CampaignRecipients
            .AsNoTracking()
            .Where(recipient =>
                recipient.CampaignId == campaignId
                && recipient.Status == CampaignRecipientStatus.Pending)
            .OrderBy(recipient => recipient.Id)
            .Take(batchSize)
            .Select(recipient => new CampaignSendBatchRecord(
                recipient.Id,
                recipient.PhoneNumberRecordId,
                recipient.PhoneNumberRecord.CanonicalPhoneNumber))
            .ToListAsync(cancellationToken);
    }
}

public sealed record CampaignSendBatchRecord(
    int CampaignRecipientId,
    int PhoneNumberRecordId,
    string CanonicalPhoneNumber);
