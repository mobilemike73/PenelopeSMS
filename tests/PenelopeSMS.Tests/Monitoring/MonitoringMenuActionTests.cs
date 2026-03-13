using PenelopeSMS.App.Menu;
using PenelopeSMS.App.Rendering;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.SqlServer.Queries;

namespace PenelopeSMS.Tests.Monitoring;

public sealed class MonitoringMenuActionTests
{
    [Fact]
    public async Task ExecuteAsyncTogglesCompletedCampaignVisibility()
    {
        var workflow = new FakeMonitoringWorkflow(
            hiddenDashboard: CreateDashboard(includeCompletedCampaigns: false),
            visibleDashboard: CreateDashboard(includeCompletedCampaigns: true),
            detail: CreateDetail());
        var output = new StringWriter();
        var action = new MonitoringMenuAction(
            workflow,
            new MonitoringScreenRenderer(),
            new StringReader("c\n0\n"),
            output,
            new FixedTimeProvider(new DateTimeOffset(2026, 03, 13, 12, 30, 00, TimeSpan.Zero)),
            TimeSpan.FromMinutes(1));

        await action.ExecuteAsync();

        var renderedOutput = output.ToString();
        var shownMarkerIndex = renderedOutput.IndexOf("Completed campaigns: shown", StringComparison.Ordinal);
        var hiddenSegment = shownMarkerIndex >= 0
            ? renderedOutput[..shownMarkerIndex]
            : renderedOutput;
        var shownSegment = shownMarkerIndex >= 0
            ? renderedOutput[shownMarkerIndex..]
            : string.Empty;

        Assert.Equal([false, true], workflow.DashboardRequests);
        Assert.Contains("Completed campaigns: hidden", renderedOutput, StringComparison.Ordinal);
        Assert.Contains("Completed campaigns: shown", renderedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("[2] Archived campaign", hiddenSegment, StringComparison.Ordinal);
        Assert.Contains("[2] Archived campaign", shownSegment, StringComparison.Ordinal);
        Assert.Contains("Warnings", renderedOutput, StringComparison.Ordinal);
        Assert.Contains("Live delivery", renderedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsyncShowsCampaignDrillInWithIssueSample()
    {
        var workflow = new FakeMonitoringWorkflow(
            hiddenDashboard: CreateDashboard(includeCompletedCampaigns: false),
            visibleDashboard: CreateDashboard(includeCompletedCampaigns: true),
            detail: CreateDetail());
        var output = new StringWriter();
        var action = new MonitoringMenuAction(
            workflow,
            new MonitoringScreenRenderer(),
            new StringReader("1\n0\n0\n"),
            output,
            new FixedTimeProvider(new DateTimeOffset(2026, 03, 13, 12, 30, 00, TimeSpan.Zero)),
            TimeSpan.FromMinutes(1));

        await action.ExecuteAsync();

        Assert.Equal([1], workflow.DetailRequests);
        Assert.Contains("Campaign Detail [1] Active campaign", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Pending: 4 | Submitted: 2 | Queued: 1 | Sent: 1", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("+16502530099", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Provider timeout", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsyncExportsHtmlReport()
    {
        var workflow = new FakeMonitoringWorkflow(
            hiddenDashboard: CreateDashboard(includeCompletedCampaigns: false),
            visibleDashboard: CreateDashboard(includeCompletedCampaigns: true),
            detail: CreateDetail(),
            exportPath: @"C:\reports\penelope-monitoring-report.html");
        var output = new StringWriter();
        var action = new MonitoringMenuAction(
            workflow,
            new MonitoringScreenRenderer(),
            new StringReader("e\n\n0\n"),
            output,
            new FixedTimeProvider(new DateTimeOffset(2026, 03, 13, 12, 30, 00, TimeSpan.Zero)),
            TimeSpan.FromMinutes(1));

        await action.ExecuteAsync();

        Assert.Equal(1, workflow.ExportRequests);
        Assert.Contains("HTML report written to C:\\reports\\penelope-monitoring-report.html", output.ToString(), StringComparison.Ordinal);
    }

    private static MonitoringDashboardSnapshot CreateDashboard(bool includeCompletedCampaigns)
    {
        var campaigns = new List<CampaignMonitoringSummaryRecord>
        {
            new(
                1,
                "Active campaign",
                100,
                CampaignStatus.Sending,
                4,
                2,
                1,
                1,
                5,
                0,
                1,
                new DateTime(2026, 03, 13, 12, 10, 00, DateTimeKind.Utc))
        };

        if (includeCompletedCampaigns)
        {
            campaigns.Add(new CampaignMonitoringSummaryRecord(
                2,
                "Archived campaign",
                100,
                CampaignStatus.Completed,
                0,
                0,
                0,
                0,
                10,
                0,
                0,
                new DateTime(2026, 03, 13, 11, 50, 00, DateTimeKind.Utc)));
        }

        return new MonitoringDashboardSnapshot(
            Campaigns: campaigns,
            CompletedJobs:
            [
                new MonitoringCompletedJobRecord(
                    "enrichment:1",
                    "enrichment",
                    "Due-record enrichment",
                    "Completed",
                    new DateTime(2026, 03, 13, 12, 05, 00, DateTimeKind.Utc),
                    "Enrichment complete. Selected: 3, Processed: 3, Updated: 2, Failed: 1",
                    IsLiveSession: true)
            ],
            PersistedIssues:
            [
                new PersistedMonitoringIssueRecord(
                    "callback_issues",
                    "Callback issues",
                    2,
                    new DateTime(2026, 03, 13, 12, 07, 00, DateTimeKind.Utc),
                    "Unmatched: 1, Rejected: 1")
            ],
            ActiveJobs:
            [
                new MonitoringActiveJobRecord(
                    "job-1",
                    "Delivery processing",
                    "Delivery callback processing",
                    "Active",
                    new DateTime(2026, 03, 13, 12, 00, 00, DateTimeKind.Utc),
                    "Waiting for queue messages")
            ],
            ActiveWarnings:
            [
                new MonitoringWarningRecord(
                    "warning-1",
                    "Import",
                    "Oracle import is retrying after timeout.",
                    new DateTime(2026, 03, 13, 12, 08, 00, DateTimeKind.Utc))
            ],
            LiveDeliveryLines:
            [
                "[12:09:00] Deleted queue message message-1 after applied."
            ]);
    }

    private static CampaignMonitoringDetailRecord CreateDetail()
    {
        return new CampaignMonitoringDetailRecord(
            new CampaignMonitoringSummaryRecord(
                1,
                "Active campaign",
                100,
                CampaignStatus.Sending,
                4,
                2,
                1,
                1,
                5,
                0,
                1,
                new DateTime(2026, 03, 13, 12, 10, 00, DateTimeKind.Utc)),
            [
                new CampaignRecipientIssueRecord(
                    99,
                    "+16502530099",
                    CampaignRecipientStatus.Failed,
                    "SM-99",
                    "30001",
                    "Provider timeout",
                    new DateTime(2026, 03, 13, 12, 09, 00, DateTimeKind.Utc))
            ]);
    }

    private sealed class FakeMonitoringWorkflow(
        MonitoringDashboardSnapshot hiddenDashboard,
        MonitoringDashboardSnapshot visibleDashboard,
        CampaignMonitoringDetailRecord detail,
        string exportPath = "/tmp/report.html") : IMonitoringWorkflow
    {
        public List<bool> DashboardRequests { get; } = [];

        public List<int> DetailRequests { get; } = [];

        public int ExportRequests { get; private set; }

        public Task<MonitoringDashboardSnapshot> GetDashboardAsync(
            bool includeCompletedCampaigns = false,
            CancellationToken cancellationToken = default)
        {
            DashboardRequests.Add(includeCompletedCampaigns);
            return Task.FromResult(includeCompletedCampaigns ? visibleDashboard : hiddenDashboard);
        }

        public Task<CampaignMonitoringDetailRecord> GetCampaignDetailAsync(
            int campaignId,
            CancellationToken cancellationToken = default)
        {
            DetailRequests.Add(campaignId);
            return Task.FromResult(detail);
        }

        public Task<string> ExportHtmlReportAsync(
            string? outputPath = null,
            CancellationToken cancellationToken = default)
        {
            ExportRequests++;
            return Task.FromResult(outputPath ?? exportPath);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
