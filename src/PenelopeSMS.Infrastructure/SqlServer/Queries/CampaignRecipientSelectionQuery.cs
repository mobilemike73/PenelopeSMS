using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Infrastructure.SqlServer.Queries;

public sealed class CampaignRecipientSelectionQuery(PenelopeSmsDbContext dbContext)
{
    public Task<int> CountImportedPhoneNumbersAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.PhoneNumberRecords.CountAsync(cancellationToken);
    }

    public Task<List<CampaignRecipientSelectionRecord>> ListEligibleRecipientsAsync(
        CancellationToken cancellationToken = default)
    {
        return dbContext.PhoneNumberRecords
            .AsNoTracking()
            .Where(record => record.CampaignEligibilityStatus == CampaignEligibilityStatus.Eligible)
            .OrderBy(record => record.Id)
            .Select(record => new CampaignRecipientSelectionRecord(
                record.Id,
                record.CanonicalPhoneNumber))
            .ToListAsync(cancellationToken);
    }
}

public sealed record CampaignRecipientSelectionRecord(
    int PhoneNumberRecordId,
    string CanonicalPhoneNumber);
