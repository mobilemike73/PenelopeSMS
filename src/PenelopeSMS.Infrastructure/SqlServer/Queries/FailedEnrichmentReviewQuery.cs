using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Infrastructure.SqlServer.Queries;

public sealed class FailedEnrichmentReviewQuery(PenelopeSmsDbContext dbContext)
{
    public Task<List<FailedEnrichmentReviewRecord>> ListFailuresAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.PhoneNumberRecords
            .AsNoTracking()
            .Where(record => record.EnrichmentFailureStatus != EnrichmentFailureStatus.None)
            .OrderByDescending(record => record.LastEnrichmentFailedAtUtc)
            .ThenBy(record => record.Id)
            .Select(record => new FailedEnrichmentReviewRecord(
                record.Id,
                record.CanonicalPhoneNumber,
                record.EnrichmentFailureStatus,
                record.LastEnrichmentFailedAtUtc,
                record.LastEnrichmentErrorCode,
                record.LastEnrichmentErrorMessage,
                record.CampaignEligibilityStatus))
            .ToListAsync(cancellationToken);
    }

    public Task<List<EnrichmentTargetRecord>> ListRetryableTargetsAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.PhoneNumberRecords
            .AsNoTracking()
            .Where(record => record.EnrichmentFailureStatus == EnrichmentFailureStatus.Retryable)
            .OrderBy(record => record.Id)
            .Select(record => new EnrichmentTargetRecord(record.Id, record.CanonicalPhoneNumber))
            .ToListAsync(cancellationToken);
    }

    public Task<List<EnrichmentTargetRecord>> ListRetryableTargetsByIdsAsync(
        IReadOnlyCollection<int> phoneNumberRecordIds,
        CancellationToken cancellationToken = default)
    {
        if (phoneNumberRecordIds.Count == 0)
        {
            return Task.FromResult(new List<EnrichmentTargetRecord>());
        }

        return dbContext.PhoneNumberRecords
            .AsNoTracking()
            .Where(record =>
                phoneNumberRecordIds.Contains(record.Id)
                && record.EnrichmentFailureStatus == EnrichmentFailureStatus.Retryable)
            .OrderBy(record => record.Id)
            .Select(record => new EnrichmentTargetRecord(record.Id, record.CanonicalPhoneNumber))
            .ToListAsync(cancellationToken);
    }
}

public sealed record FailedEnrichmentReviewRecord(
    int PhoneNumberRecordId,
    string CanonicalPhoneNumber,
    EnrichmentFailureStatus FailureStatus,
    DateTime? LastEnrichmentFailedAtUtc,
    string? ErrorCode,
    string? ErrorMessage,
    CampaignEligibilityStatus EligibilityStatus)
{
    public bool IsRetryable => FailureStatus == EnrichmentFailureStatus.Retryable;
}
