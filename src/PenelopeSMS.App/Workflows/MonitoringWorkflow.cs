using System.Text;
using PenelopeSMS.App.Monitoring;
using PenelopeSMS.App.Rendering;
using PenelopeSMS.Infrastructure.SqlServer.Queries;

namespace PenelopeSMS.App.Workflows;

public sealed class MonitoringWorkflow(
    CampaignMonitoringQuery campaignMonitoringQuery,
    OperationsIssueQuery operationsIssueQuery,
    MonitoringHtmlReportQuery monitoringHtmlReportQuery,
    MonitoringHtmlReportRenderer monitoringHtmlReportRenderer,
    IOperationsMonitor operationsMonitor,
    TimeProvider? timeProvider = null) : IMonitoringWorkflow
{
    private readonly TimeProvider reportTimeProvider = timeProvider ?? TimeProvider.System;

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

    public async Task<string> ExportHtmlReportAsync(
        string? outputPath = null,
        CancellationToken cancellationToken = default)
    {
        var generatedAtUtc = reportTimeProvider.GetUtcNow().UtcDateTime;
        var dashboard = await GetDashboardAsync(
            includeCompletedCampaigns: true,
            cancellationToken);
        var reportData = await monitoringHtmlReportQuery.GetReportDataAsync(cancellationToken);
        var html = monitoringHtmlReportRenderer.Render(new MonitoringHtmlReportDocument(
            GeneratedAtUtc: generatedAtUtc,
            Dashboard: dashboard,
            ReportData: reportData));

        var resolvedOutputPath = ResolveOutputPath(outputPath, generatedAtUtc);
        var directoryPath = Path.GetDirectoryName(resolvedOutputPath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await File.WriteAllTextAsync(
            resolvedOutputPath,
            html,
            Encoding.UTF8,
            cancellationToken);

        return resolvedOutputPath;
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

    private static string ResolveOutputPath(string? outputPath, DateTime generatedAtUtc)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            return Path.GetFullPath(outputPath);
        }

        var fileName = $"penelope-monitoring-report-{generatedAtUtc:yyyyMMdd-HHmmss}.html";
        return Path.Combine(Directory.GetCurrentDirectory(), "reports", fileName);
    }
}
