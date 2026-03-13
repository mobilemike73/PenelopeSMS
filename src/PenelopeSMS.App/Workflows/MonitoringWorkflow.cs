using PenelopeSMS.App.Monitoring;
using PenelopeSMS.Infrastructure.SqlServer.Queries;

namespace PenelopeSMS.App.Workflows;

public sealed class MonitoringWorkflow(
    CampaignMonitoringQuery campaignMonitoringQuery,
    OperationsIssueQuery operationsIssueQuery,
    IOperationsMonitor operationsMonitor) : IMonitoringWorkflow
{
    public async Task<MonitoringDashboardSnapshot> GetDashboardAsync(
        bool includeCompletedCampaigns = false,
        CancellationToken cancellationToken = default)
    {
        var campaigns = await campaignMonitoringQuery.ListCampaignsAsync(
            includeCompletedCampaigns,
            cancellationToken);
        var persistedCompletedJobs = await operationsIssueQuery.ListRecentCompletedJobsAsync(cancellationToken);
        var persistedIssues = await operationsIssueQuery.ListActiveIssuesAsync(cancellationToken);
        var runtimeSnapshot = operationsMonitor.GetSnapshot();

        return new MonitoringDashboardSnapshot(
            Campaigns: campaigns,
            CompletedJobs: MergeCompletedJobs(persistedCompletedJobs, runtimeSnapshot),
            PersistedIssues: persistedIssues,
            ActiveJobs: runtimeSnapshot.ActiveJobs
                .Select(activeJob => new MonitoringActiveJobRecord(
                    activeJob.JobId,
                    FormatOperationType(activeJob.OperationType),
                    activeJob.Label,
                    "Active",
                    activeJob.StartedAtUtc,
                    activeJob.ProgressDetail))
                .ToList(),
            ActiveWarnings: runtimeSnapshot.ActiveWarnings
                .Select(warning => new MonitoringWarningRecord(
                    warning.WarningId,
                    FormatOperationType(warning.OperationType),
                    warning.Message,
                    warning.CreatedAtUtc))
                .ToList(),
            LiveDeliveryLines: runtimeSnapshot.LiveDeliveryLines);
    }

    public async Task<CampaignMonitoringDetailRecord> GetCampaignDetailAsync(
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        var campaign = await campaignMonitoringQuery.GetCampaignDetailAsync(campaignId, cancellationToken);

        return campaign
            ?? throw new InvalidOperationException($"Campaign {campaignId} was not found.");
    }

    private static List<MonitoringCompletedJobRecord> MergeCompletedJobs(
        IReadOnlyList<PersistedCompletedJobRecord> persistedCompletedJobs,
        OperationSnapshot runtimeSnapshot)
    {
        var mergedJobs = persistedCompletedJobs
            .Select(job => new MonitoringCompletedJobRecord(
                job.JobKey,
                job.JobType,
                job.Label,
                job.Outcome,
                job.CompletedAtUtc,
                job.Summary,
                IsLiveSession: false))
            .ToList();

        foreach (var runtimeJob in runtimeSnapshot.CompletedJobs)
        {
            var mappedRuntimeJob = new MonitoringCompletedJobRecord(
                runtimeJob.JobId,
                GetOperationKey(runtimeJob.OperationType),
                runtimeJob.Label,
                runtimeJob.Outcome,
                runtimeJob.CompletedAtUtc,
                runtimeJob.Summary,
                IsLiveSession: true);

            var isDuplicate = mergedJobs.Any(job =>
                job.JobType == mappedRuntimeJob.JobType
                && job.Label == mappedRuntimeJob.Label
                && string.Equals(job.Outcome, mappedRuntimeJob.Outcome, StringComparison.OrdinalIgnoreCase)
                && Math.Abs((job.CompletedAtUtc - mappedRuntimeJob.CompletedAtUtc).TotalMinutes) <= 5);

            if (!isDuplicate)
            {
                mergedJobs.Add(mappedRuntimeJob);
            }
        }

        return mergedJobs
            .OrderByDescending(job => job.CompletedAtUtc)
            .ThenBy(job => job.JobKey)
            .Take(10)
            .ToList();
    }

    private static string FormatOperationType(OperationType operationType)
    {
        return operationType switch
        {
            OperationType.Import => "Import",
            OperationType.Enrichment => "Enrichment",
            OperationType.CampaignSend => "Campaign send",
            OperationType.DeliveryProcessing => "Delivery processing",
            _ => operationType.ToString()
        };
    }

    private static string GetOperationKey(OperationType operationType)
    {
        return operationType switch
        {
            OperationType.Import => "import",
            OperationType.Enrichment => "enrichment",
            OperationType.CampaignSend => "campaign_send",
            OperationType.DeliveryProcessing => "delivery_processing",
            _ => operationType.ToString().ToLowerInvariant()
        };
    }
}
