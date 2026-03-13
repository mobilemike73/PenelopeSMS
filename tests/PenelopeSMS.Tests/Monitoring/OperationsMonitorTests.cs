using PenelopeSMS.App.Monitoring;

namespace PenelopeSMS.Tests.Monitoring;

public sealed class OperationsMonitorTests
{
    [Fact]
    public void CompleteJobMovesItToCompletedAndClearsAttachedWarnings()
    {
        var monitor = new OperationsMonitor();
        var jobId = monitor.StartJob(OperationType.Import, "Oracle import");
        monitor.Warn(OperationType.Import, "Rejected row", jobId, new DateTime(2026, 03, 13, 12, 00, 00, DateTimeKind.Utc));

        monitor.CompleteJob(jobId, "Finished");
        var snapshot = monitor.GetSnapshot();

        Assert.Empty(snapshot.ActiveJobs);
        Assert.Empty(snapshot.ActiveWarnings);
        Assert.Single(snapshot.CompletedJobs);
        Assert.Equal("Completed", snapshot.CompletedJobs[0].Outcome);
    }

    [Fact]
    public void WarnOrdersActiveWarningsNewestFirst()
    {
        var monitor = new OperationsMonitor();

        monitor.Warn(OperationType.Enrichment, "Older", createdAtUtc: new DateTime(2026, 03, 13, 10, 00, 00, DateTimeKind.Utc));
        monitor.Warn(OperationType.Import, "Newer", createdAtUtc: new DateTime(2026, 03, 13, 11, 00, 00, DateTimeKind.Utc));

        var snapshot = monitor.GetSnapshot();

        Assert.Equal(["Newer", "Older"], snapshot.ActiveWarnings.Select(warning => warning.Message));
    }

    [Fact]
    public void ResolveWarningsRemovesMatchingWarningsFromActiveView()
    {
        var monitor = new OperationsMonitor();
        var jobId = monitor.StartJob(OperationType.CampaignSend, "Campaign send");
        monitor.Warn(OperationType.CampaignSend, "Provider failure", jobId);

        monitor.ResolveWarnings(jobId: jobId);

        Assert.Empty(monitor.GetSnapshot().ActiveWarnings);
    }

    [Fact]
    public void RecordLiveDeliveryLineKeepsOnlyBoundedRecentBuffer()
    {
        var monitor = new OperationsMonitor();

        for (var index = 0; index < 30; index++)
        {
            monitor.RecordLiveDeliveryLine($"line-{index}");
        }

        var snapshot = monitor.GetSnapshot();

        Assert.Equal(25, snapshot.LiveDeliveryLines.Count);
        Assert.Equal("line-5", snapshot.LiveDeliveryLines[0]);
        Assert.Equal("line-29", snapshot.LiveDeliveryLines[^1]);
    }
}
