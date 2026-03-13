using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Infrastructure.SqlServer.Queries;

public sealed class EnrichmentTargetingQuery(PenelopeSmsDbContext dbContext)
{
    private const int FreshnessWindowDays = 30;

    public Task<int> CountImportedPhoneNumbersAsync(
        CustomerSegment customerSegment,
        CancellationToken cancellationToken = default)
    {
        return CreateSegmentQuery(customerSegment).CountAsync(cancellationToken);
    }

    public Task<List<EnrichmentTargetRecord>> ListTargetsAsync(
        CustomerSegment customerSegment,
        bool fullRefresh,
        CancellationToken cancellationToken = default)
    {
        var query = CreateSegmentQuery(customerSegment)
            .AsNoTracking()
            .OrderBy(record => record.Id)
            .AsQueryable();

        if (!fullRefresh)
        {
            var staleThresholdUtc = DateTime.UtcNow.AddDays(-FreshnessWindowDays);

            query = query.Where(record =>
                record.LastEnrichedAtUtc == null
                || record.EnrichmentFailureStatus != EnrichmentFailureStatus.None
                || (record.EnrichmentFailureStatus == EnrichmentFailureStatus.None
                    && record.LastEnrichedAtUtc <= staleThresholdUtc));
        }

        return query
            .Select(record => new EnrichmentTargetRecord(record.Id, record.CanonicalPhoneNumber))
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

public sealed record EnrichmentTargetRecord(
    int PhoneNumberRecordId,
    string CanonicalPhoneNumber);
