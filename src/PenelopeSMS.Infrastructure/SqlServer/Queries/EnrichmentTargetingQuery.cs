using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Infrastructure.SqlServer.Queries;

public sealed class EnrichmentTargetingQuery(PenelopeSmsDbContext dbContext)
{
    private const int FreshnessWindowDays = 30;

    public Task<int> CountImportedPhoneNumbersAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.PhoneNumberRecords.CountAsync(cancellationToken);
    }

    public Task<List<EnrichmentTargetRecord>> ListTargetsAsync(
        bool fullRefresh,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.PhoneNumberRecords
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
}

public sealed record EnrichmentTargetRecord(
    int PhoneNumberRecordId,
    string CanonicalPhoneNumber);
