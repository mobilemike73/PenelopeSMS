using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Infrastructure.SqlServer.Queries;

public sealed class OperationsIssueQuery(PenelopeSmsDbContext dbContext)
{
    public async Task<IReadOnlyList<PersistedMonitoringIssueRecord>> ListActiveIssuesAsync(
        CancellationToken cancellationToken = default)
    {
        var issues = new List<PersistedMonitoringIssueRecord>();

        var unmatchedCallbacks = await dbContext.UnmatchedDeliveryCallbacks
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var rejectedCallbacks = await dbContext.RejectedDeliveryCallbacks
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var callbackIssueCount = unmatchedCallbacks.Count + rejectedCallbacks.Count;

        if (callbackIssueCount > 0)
        {
            issues.Add(new PersistedMonitoringIssueRecord(
                Category: "callback_issues",
                Label: "Callback issues",
                Count: callbackIssueCount,
                LastOccurredAtUtc: Max(
                    unmatchedCallbacks.Max(callback => (DateTime?)callback.LastSeenAtUtc),
                    rejectedCallbacks.Max(callback => (DateTime?)callback.LastSeenAtUtc)),
                Detail: $"Unmatched: {unmatchedCallbacks.Count}, Rejected: {rejectedCallbacks.Count}"));
        }

        var enrichmentFailures = await dbContext.PhoneNumberRecords
            .AsNoTracking()
            .Where(record => record.EnrichmentFailureStatus != EnrichmentFailureStatus.None)
            .Select(record => new
            {
                record.EnrichmentFailureStatus,
                record.LastEnrichmentFailedAtUtc
            })
            .ToListAsync(cancellationToken);

        if (enrichmentFailures.Count > 0)
        {
            issues.Add(new PersistedMonitoringIssueRecord(
                Category: "enrichment_failures",
                Label: "Enrichment failures",
                Count: enrichmentFailures.Count,
                LastOccurredAtUtc: enrichmentFailures.Max(record => record.LastEnrichmentFailedAtUtc),
                Detail: $"Retryable: {enrichmentFailures.Count(record => record.EnrichmentFailureStatus == EnrichmentFailureStatus.Retryable)}, Permanent: {enrichmentFailures.Count(record => record.EnrichmentFailureStatus == EnrichmentFailureStatus.Permanent)}"));
        }

        var failedImports = await dbContext.ImportBatches
            .AsNoTracking()
            .Where(batch => batch.Status == ImportBatch.FailedStatus)
            .Select(batch => new
            {
                batch.CompletedAtUtc,
                batch.StartedAtUtc
            })
            .ToListAsync(cancellationToken);

        if (failedImports.Count > 0)
        {
            issues.Add(new PersistedMonitoringIssueRecord(
                Category: "failed_imports",
                Label: "Failed imports",
                Count: failedImports.Count,
                LastOccurredAtUtc: failedImports.Max(batch => batch.CompletedAtUtc ?? batch.StartedAtUtc),
                Detail: $"{failedImports.Count} failed import batch(es) on record"));
        }

        return issues
            .OrderByDescending(issue => issue.LastOccurredAtUtc)
            .ThenByDescending(issue => issue.Count)
            .ToList();
    }

    public async Task<IReadOnlyList<PersistedCompletedJobRecord>> ListRecentCompletedJobsAsync(
        CancellationToken cancellationToken = default)
    {
        var importJobs = await dbContext.ImportBatches
            .AsNoTracking()
            .Where(batch => batch.Status == ImportBatch.CompletedStatus || batch.Status == ImportBatch.FailedStatus)
            .Select(batch => new PersistedCompletedJobRecord(
                "import",
                $"import:{batch.Id}",
                $"Import batch {batch.Id}",
                batch.Status,
                batch.CompletedAtUtc ?? batch.StartedAtUtc,
                $"Read: {batch.RowsRead}, Imported: {batch.RowsImported}, Rejected: {batch.RowsRejected}"))
            .ToListAsync(cancellationToken);

        var sendJobs = await dbContext.Campaigns
            .AsNoTracking()
            .Where(campaign => campaign.StartedAtUtc != null)
            .Select(campaign => new PersistedCompletedJobRecord(
                "campaign_send",
                $"campaign:{campaign.Id}",
                $"Campaign {campaign.Name}",
                campaign.Status == CampaignStatus.Completed ? "Completed" : "InProgress",
                campaign.CompletedAtUtc ?? campaign.StartedAtUtc!.Value,
                $"Pending: {campaign.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Pending)}, Submitted: {campaign.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Submitted)}, Failed: {campaign.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Failed)}"))
            .ToListAsync(cancellationToken);

        return importJobs
            .Concat(sendJobs)
            .OrderByDescending(job => job.CompletedAtUtc)
            .ThenBy(job => job.JobKey)
            .Take(10)
            .ToList();
    }

    private static DateTime? Max(DateTime? left, DateTime? right)
    {
        if (!left.HasValue)
        {
            return right;
        }

        if (!right.HasValue)
        {
            return left;
        }

        return left > right ? left : right;
    }
}

public sealed record PersistedMonitoringIssueRecord(
    string Category,
    string Label,
    int Count,
    DateTime? LastOccurredAtUtc,
    string Detail);

public sealed record PersistedCompletedJobRecord(
    string JobType,
    string JobKey,
    string Label,
    string Outcome,
    DateTime CompletedAtUtc,
    string Summary);
