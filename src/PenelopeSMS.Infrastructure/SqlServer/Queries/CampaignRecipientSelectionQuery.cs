using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Infrastructure.SqlServer.Queries;

public sealed class CampaignRecipientSelectionQuery(PenelopeSmsDbContext dbContext)
{
    public Task<int> CountImportedPhoneNumbersAsync(
        CustomerSegment customerSegment,
        CancellationToken cancellationToken = default)
    {
        return CreateSegmentQuery(customerSegment).CountAsync(cancellationToken);
    }

    public Task<List<CampaignRecipientSelectionRecord>> ListEligibleRecipientsAsync(
        CustomerSegment customerSegment,
        CancellationToken cancellationToken = default)
    {
        return CreateSegmentQuery(customerSegment)
            .AsNoTracking()
            .Where(record => record.CampaignEligibilityStatus == CampaignEligibilityStatus.Eligible)
            .OrderBy(record => record.Id)
            .Select(record => new CampaignRecipientSelectionRecord(
                record.Id,
                record.CanonicalPhoneNumber))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<PenelopeSMS.Domain.Entities.PhoneNumberRecord> CreateSegmentQuery(CustomerSegment customerSegment)
    {
        var query = dbContext.PhoneNumberRecords.AsQueryable();

        return customerSegment == CustomerSegment.Vip
            ? query.Where(record => record.CustomerPhoneLinks.Any(link => link.IsVip))
            : query.Where(record => !record.CustomerPhoneLinks.Any(link => link.IsVip));
    }
}

public sealed record CampaignRecipientSelectionRecord(
    int PhoneNumberRecordId,
    string CanonicalPhoneNumber);
