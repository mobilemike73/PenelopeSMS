using PenelopeSMS.Infrastructure.SqlServer.Queries;

namespace PenelopeSMS.App.Workflows;

public interface IMonitoringWorkflow
{
    Task<MonitoringDashboardSnapshot> GetDashboardAsync(
        bool includeCompletedCampaigns = false,
        CancellationToken cancellationToken = default);

    Task<CampaignMonitoringDetailRecord> GetCampaignDetailAsync(
        int campaignId,
        CancellationToken cancellationToken = default);

    Task<string> ExportHtmlReportAsync(
        string? outputPath = null,
        CancellationToken cancellationToken = default);
}

public sealed record MonitoringDashboardSnapshot(
    IReadOnlyList<CampaignMonitoringSummaryRecord> Campaigns,
    IReadOnlyList<MonitoringCompletedJobRecord> CompletedJobs,
    IReadOnlyList<PersistedMonitoringIssueRecord> PersistedIssues,
    IReadOnlyList<MonitoringActiveJobRecord>? ActiveJobs = null,
    IReadOnlyList<MonitoringWarningRecord>? ActiveWarnings = null,
    IReadOnlyList<string>? LiveDeliveryLines = null,
    MonitoringQueueStatusRecord? QueueStatus = null,
    MonitoringQueueRatesRecord? QueueRates = null,
    MonitoringQueueRatesRecord? TwilioInFlightRates = null)
{
    public IReadOnlyList<MonitoringActiveJobRecord> ActiveJobsOrEmpty => ActiveJobs ?? [];

    public IReadOnlyList<MonitoringWarningRecord> ActiveWarningsOrEmpty => ActiveWarnings ?? [];

    public IReadOnlyList<string> LiveDeliveryLinesOrEmpty => LiveDeliveryLines ?? [];
}

public sealed record MonitoringActiveJobRecord(
    string JobId,
    string JobType,
    string Label,
    string State,
    DateTime StartedAtUtc,
    string? ProgressDetail);

public sealed record MonitoringWarningRecord(
    string WarningId,
    string Source,
    string Message,
    DateTime CreatedAtUtc);

public sealed record MonitoringCompletedJobRecord(
    string JobKey,
    string JobType,
    string Label,
    string Outcome,
    DateTime CompletedAtUtc,
    string Summary,
    bool IsLiveSession);

public sealed record MonitoringQueueStatusRecord(
    int VisibleMessages,
    int MessagesInFlight);

public sealed record MonitoringQueueRatesRecord(
    double? CurrentMessagesPerSecond,
    double? OneMinuteAverageMessagesPerSecond,
    double? FiveMinuteAverageMessagesPerSecond,
    double? TenMinuteAverageMessagesPerSecond);
