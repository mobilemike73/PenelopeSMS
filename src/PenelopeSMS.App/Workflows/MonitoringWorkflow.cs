using PenelopeSMS.Infrastructure.SqlServer.Queries;

namespace PenelopeSMS.App.Workflows;

public sealed class MonitoringWorkflow(
    CampaignMonitoringQuery campaignMonitoringQuery,
    OperationsIssueQuery operationsIssueQuery) : IMonitoringWorkflow
{
    public async Task<MonitoringDashboardSnapshot> GetDashboardAsync(
        bool includeCompletedCampaigns = false,
        CancellationToken cancellationToken = default)
    {
        var campaigns = await campaignMonitoringQuery.ListCampaignsAsync(
            includeCompletedCampaigns,
            cancellationToken);
        var completedJobs = await operationsIssueQuery.ListRecentCompletedJobsAsync(cancellationToken);
        var persistedIssues = await operationsIssueQuery.ListActiveIssuesAsync(cancellationToken);

        return new MonitoringDashboardSnapshot(
            Campaigns: campaigns,
            CompletedJobs: completedJobs,
            PersistedIssues: persistedIssues);
    }

    public async Task<CampaignMonitoringDetailRecord> GetCampaignDetailAsync(
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        var campaign = await campaignMonitoringQuery.GetCampaignDetailAsync(campaignId, cancellationToken);

        return campaign
            ?? throw new InvalidOperationException($"Campaign {campaignId} was not found.");
    }
}
