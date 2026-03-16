using System.Globalization;
using System.Text;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Infrastructure.SqlServer.Queries;

namespace PenelopeSMS.App.Rendering;

public sealed class MonitoringScreenRenderer
{
    private const string ClearScreenSequence = "\u001b[2J\u001b[H";
    private readonly string clearScreenSequence = ClearScreenSequence;

    public async Task WriteDashboardAsync(
        TextWriter output,
        MonitoringDashboardSnapshot snapshot,
        bool includeCompletedCampaigns,
        DateTime refreshedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(output);

        await output.WriteAsync(RenderDashboard(snapshot, includeCompletedCampaigns, refreshedAtUtc));
        await output.FlushAsync();
    }

    public async Task WriteCampaignDetailAsync(
        TextWriter output,
        CampaignMonitoringDetailRecord detail,
        DateTime refreshedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(output);

        await output.WriteAsync(RenderCampaignDetail(detail, refreshedAtUtc));
        await output.FlushAsync();
    }

    public string RenderDashboard(
        MonitoringDashboardSnapshot snapshot,
        bool includeCompletedCampaigns,
        DateTime refreshedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var builder = new StringBuilder();
        builder.Append(clearScreenSequence);
        builder.AppendLine("Operations Monitor");
        builder.AppendLine(Invariant($"Refreshed: {refreshedAtUtc:yyyy-MM-dd HH:mm:ss} UTC"));
        builder.AppendLine(includeCompletedCampaigns
            ? "Completed campaigns: shown"
            : "Completed campaigns: hidden (enter 'c' to reveal)");
        builder.AppendLine("Commands: [campaign id] drill-in | e export HTML report | c toggle completed | r refresh | 0 back");
        builder.AppendLine();

        AppendQueueStatus(builder, snapshot);
        AppendTwilioInFlight(builder, snapshot);
        AppendActiveJobs(builder, snapshot.ActiveJobsOrEmpty);
        AppendWarnings(builder, snapshot);
        AppendCampaigns(builder, snapshot.Campaigns, includeCompletedCampaigns);
        AppendCompletedJobs(builder, snapshot.CompletedJobs);
        AppendLiveDelivery(builder, snapshot.LiveDeliveryLinesOrEmpty);

        return builder.ToString();
    }

    public string RenderCampaignDetail(
        CampaignMonitoringDetailRecord detail,
        DateTime refreshedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var summary = detail.Summary;
        var builder = new StringBuilder();
        builder.Append(clearScreenSequence);
        builder.AppendLine(Invariant($"Campaign Detail [{summary.CampaignId}] {summary.CampaignName}"));
        builder.AppendLine(Invariant($"Refreshed: {refreshedAtUtc:yyyy-MM-dd HH:mm:ss} UTC"));
        builder.AppendLine(Invariant($"Status: {summary.Status} | Batch size: {summary.BatchSize} | Last activity: {summary.LastActivityAtUtc:yyyy-MM-dd HH:mm:ss} UTC"));
        builder.AppendLine("Commands: r refresh | 0 back");
        builder.AppendLine();

        builder.AppendLine("Status totals");
        builder.AppendLine(Invariant($"Pending: {summary.PendingRecipients} | Submitted: {summary.SubmittedRecipients} | Queued: {summary.QueuedRecipients} | Sent: {summary.SentRecipients}"));
        builder.AppendLine(Invariant($"Delivered: {summary.DeliveredRecipients} | Undelivered: {summary.UndeliveredRecipients} | Failed: {summary.FailedRecipients}"));
        builder.AppendLine();

        builder.AppendLine("Recent issues");

        if (detail.RecentIssues.Count == 0)
        {
            builder.AppendLine("No failed or callback issue samples were recorded for this campaign.");
            return builder.ToString();
        }

        foreach (var issue in detail.RecentIssues)
        {
            builder.AppendLine(Invariant(
                $"[{issue.CampaignRecipientId}] {issue.CanonicalPhoneNumber} | {issue.Status} | {issue.OccurredAtUtc:yyyy-MM-dd HH:mm:ss} UTC | Code: {issue.ErrorCode ?? "-"} | {issue.ErrorMessage ?? "No provider detail"}"));
        }

        return builder.ToString();
    }

    private static void AppendActiveJobs(
        StringBuilder builder,
        IReadOnlyList<MonitoringActiveJobRecord> activeJobs)
    {
        builder.AppendLine("Active jobs");

        if (activeJobs.Count == 0)
        {
            builder.AppendLine("None");
            builder.AppendLine();
            return;
        }

        foreach (var job in activeJobs)
        {
            builder.AppendLine(
                Invariant($"[{job.JobType}] {job.Label} | {job.State} | Started: {job.StartedAtUtc:HH:mm:ss} UTC | {job.ProgressDetail ?? "No progress detail"}"));
        }

        builder.AppendLine();
    }

    private static void AppendTwilioInFlight(
        StringBuilder builder,
        MonitoringDashboardSnapshot snapshot)
    {
        builder.AppendLine("Twilio in-flight");

        var inFlightCampaigns = snapshot.Campaigns
            .Select(campaign => new
            {
                Campaign = campaign,
                InFlightRecipients = campaign.SubmittedRecipients + campaign.QueuedRecipients + campaign.SentRecipients
            })
            .Where(item => item.InFlightRecipients > 0)
            .OrderByDescending(item => item.InFlightRecipients)
            .ThenByDescending(item => item.Campaign.LastActivityAtUtc)
            .ThenByDescending(item => item.Campaign.CampaignId)
            .ToList();

        if (inFlightCampaigns.Count == 0)
        {
            builder.AppendLine("None");
            builder.AppendLine();
            return;
        }

        var totalInFlightRecipients = inFlightCampaigns.Sum(item => item.InFlightRecipients);
        builder.AppendLine(Invariant($"Total recipients awaiting terminal callback: {totalInFlightRecipients}"));
        builder.AppendLine(Invariant(
            $"Current drain rate: {FormatRate(snapshot.TwilioInFlightRates?.CurrentMessagesPerSecond)} msg/s"));
        builder.AppendLine(Invariant(
            $"1m avg: {FormatRate(snapshot.TwilioInFlightRates?.OneMinuteAverageMessagesPerSecond)} msg/s | 5m avg: {FormatRate(snapshot.TwilioInFlightRates?.FiveMinuteAverageMessagesPerSecond)} msg/s | 10m avg: {FormatRate(snapshot.TwilioInFlightRates?.TenMinuteAverageMessagesPerSecond)} msg/s"));

        foreach (var item in inFlightCampaigns)
        {
            builder.AppendLine(Invariant(
                $"[{item.Campaign.CampaignId}] {item.Campaign.CampaignName} | In-flight: {item.InFlightRecipients} | Submitted: {item.Campaign.SubmittedRecipients} | Queued: {item.Campaign.QueuedRecipients} | Sent: {item.Campaign.SentRecipients}"));
        }

        builder.AppendLine();
    }

    private static void AppendQueueStatus(
        StringBuilder builder,
        MonitoringDashboardSnapshot snapshot)
    {
        builder.AppendLine("SQS callback queue");

        if (snapshot.QueueStatus is null)
        {
            builder.AppendLine("Unavailable");
            builder.AppendLine();
            return;
        }

        builder.AppendLine(Invariant(
            $"Visible: {snapshot.QueueStatus.VisibleMessages} | In flight: {snapshot.QueueStatus.MessagesInFlight}"));

        if (snapshot.QueueRates is null)
        {
            builder.AppendLine("Drain rate: collecting samples");
            builder.AppendLine();
            return;
        }

        builder.AppendLine(Invariant(
            $"Current drain rate: {FormatRate(snapshot.QueueRates.CurrentMessagesPerSecond)} msg/s"));
        builder.AppendLine(Invariant(
            $"1m avg: {FormatRate(snapshot.QueueRates.OneMinuteAverageMessagesPerSecond)} msg/s | 5m avg: {FormatRate(snapshot.QueueRates.FiveMinuteAverageMessagesPerSecond)} msg/s | 10m avg: {FormatRate(snapshot.QueueRates.TenMinuteAverageMessagesPerSecond)} msg/s"));
        builder.AppendLine();
    }

    private static void AppendWarnings(
        StringBuilder builder,
        MonitoringDashboardSnapshot snapshot)
    {
        builder.AppendLine("Warnings");

        var warningItems = snapshot.ActiveWarningsOrEmpty
            .Select(warning => new DashboardAlert(
                warning.CreatedAtUtc,
                warning.Source,
                warning.Message))
            .Concat(snapshot.PersistedIssues.Select(issue => new DashboardAlert(
                issue.LastOccurredAtUtc ?? DateTime.MinValue,
                issue.Label,
                $"{issue.Count} active | {issue.Detail}")))
            .OrderByDescending(alert => alert.OccurredAtUtc)
            .ThenBy(alert => alert.Source)
            .Take(10)
            .ToList();

        if (warningItems.Count == 0)
        {
            builder.AppendLine("None");
            builder.AppendLine();
            return;
        }

        foreach (var warning in warningItems)
        {
            builder.AppendLine(Invariant(
                $"{warning.OccurredAtUtc:yyyy-MM-dd HH:mm:ss} UTC | {warning.Source} | {warning.Message}"));
        }

        builder.AppendLine();
    }

    private static void AppendCampaigns(
        StringBuilder builder,
        IReadOnlyList<CampaignMonitoringSummaryRecord> campaigns,
        bool includeCompletedCampaigns)
    {
        builder.AppendLine("Campaigns");

        if (campaigns.Count == 0)
        {
            builder.AppendLine(includeCompletedCampaigns
                ? "No campaigns found."
                : "No active campaigns found. Enter 'c' to include completed campaigns.");
            builder.AppendLine();
            return;
        }

        foreach (var campaign in campaigns)
        {
            builder.AppendLine(Invariant(
                $"[{campaign.CampaignId}] {campaign.CampaignName} | {campaign.Status} | Last: {campaign.LastActivityAtUtc:yyyy-MM-dd HH:mm:ss} UTC | Pending: {campaign.PendingRecipients} | Submitted: {campaign.SubmittedRecipients} | Queued: {campaign.QueuedRecipients} | Sent: {campaign.SentRecipients} | Delivered: {campaign.DeliveredRecipients} | Undelivered: {campaign.UndeliveredRecipients} | Failed: {campaign.FailedRecipients}"));
        }

        builder.AppendLine();
    }

    private static void AppendCompletedJobs(
        StringBuilder builder,
        IReadOnlyList<MonitoringCompletedJobRecord> completedJobs)
    {
        builder.AppendLine("Recent completed jobs");

        if (completedJobs.Count == 0)
        {
            builder.AppendLine("None");
            builder.AppendLine();
            return;
        }

        foreach (var job in completedJobs)
        {
            builder.AppendLine(Invariant(
                $"{job.CompletedAtUtc:yyyy-MM-dd HH:mm:ss} UTC | {job.Label} | {job.Outcome} | {(job.IsLiveSession ? "Live session" : "Persisted")} | {job.Summary}"));
        }

        builder.AppendLine();
    }

    private static void AppendLiveDelivery(
        StringBuilder builder,
        IReadOnlyList<string> liveDeliveryLines)
    {
        builder.AppendLine("Live delivery");

        if (liveDeliveryLines.Count == 0)
        {
            builder.AppendLine("No live delivery events captured in this session.");
            return;
        }

        foreach (var line in liveDeliveryLines)
        {
            builder.AppendLine(line);
        }
    }

    private sealed record DashboardAlert(
        DateTime OccurredAtUtc,
        string Source,
        string Message);

    private static string FormatRate(double? rate)
    {
        return rate.HasValue
            ? rate.Value.ToString("0.00", CultureInfo.InvariantCulture)
            : "-";
    }

    private static string Invariant(FormattableString value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
