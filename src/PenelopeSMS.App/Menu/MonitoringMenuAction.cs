using PenelopeSMS.App.Rendering;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Infrastructure.SqlServer.Queries;

namespace PenelopeSMS.App.Menu;

public sealed class MonitoringMenuAction
{
    private static readonly TimeSpan TenMinuteWindow = TimeSpan.FromMinutes(10);
    private readonly IMonitoringWorkflow monitoringWorkflow;
    private readonly MonitoringScreenRenderer renderer;
    private readonly TextReader input;
    private readonly TextWriter output;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan refreshInterval;
    private readonly List<QueueDepthSample> queueDepthSamples = [];
    private readonly List<QueueDepthSample> twilioInFlightSamples = [];

    public MonitoringMenuAction(
        IMonitoringWorkflow monitoringWorkflow,
        MonitoringScreenRenderer renderer)
        : this(
            monitoringWorkflow,
            renderer,
            Console.In,
            Console.Out,
            TimeProvider.System,
            TimeSpan.FromSeconds(5))
    {
    }

    internal MonitoringMenuAction(
        IMonitoringWorkflow monitoringWorkflow,
        MonitoringScreenRenderer renderer,
        TextReader input,
        TextWriter output,
        TimeProvider timeProvider,
        TimeSpan refreshInterval)
    {
        this.monitoringWorkflow = monitoringWorkflow;
        this.renderer = renderer;
        this.input = input;
        this.output = output;
        this.timeProvider = timeProvider;
        this.refreshInterval = refreshInterval <= TimeSpan.Zero
            ? Timeout.InfiniteTimeSpan
            : refreshInterval;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var includeCompletedCampaigns = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            await PaintDashboardAsync(includeCompletedCampaigns, cancellationToken);
            var command = await ReadCommandWithRefreshAsync(
                () => PaintDashboardAsync(includeCompletedCampaigns, cancellationToken),
                cancellationToken);

            if (command is null)
            {
                output.WriteLine();
                return;
            }

            command = command.Trim();

            if (command == "0")
            {
                output.WriteLine();
                return;
            }

            if (command.Length == 0 || string.Equals(command, "r", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(command, "c", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "completed", StringComparison.OrdinalIgnoreCase))
            {
                includeCompletedCampaigns = !includeCompletedCampaigns;
                continue;
            }

            if (string.Equals(command, "e", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "export", StringComparison.OrdinalIgnoreCase))
            {
                var reportPath = await monitoringWorkflow.ExportHtmlReportAsync(
                    cancellationToken: cancellationToken);
                output.WriteLine($"HTML report written to {reportPath}");
                output.WriteLine("Press Enter to continue.");
                await input.ReadLineAsync(cancellationToken);
                continue;
            }

            if (int.TryParse(command, out var campaignId) && campaignId > 0)
            {
                await ShowCampaignDetailAsync(campaignId, cancellationToken);
                continue;
            }

            output.WriteLine("Unknown selection.");
            output.WriteLine("Press Enter to continue.");
            await input.ReadLineAsync(cancellationToken);
        }
    }

    private async Task ShowCampaignDetailAsync(int campaignId, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            CampaignMonitoringDetailRecord detail;

            try
            {
                detail = await monitoringWorkflow.GetCampaignDetailAsync(campaignId, cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                await output.WriteAsync($"\u001b[2J\u001b[H{exception.Message}{Environment.NewLine}Press Enter to return.{Environment.NewLine}> ");
                await output.FlushAsync(cancellationToken);
                await input.ReadLineAsync(cancellationToken);
                return;
            }

            await renderer.WriteCampaignDetailAsync(output, detail, timeProvider.GetUtcNow().UtcDateTime);
            await output.WriteAsync("> ");
            await output.FlushAsync(cancellationToken);

            var command = await ReadCommandWithRefreshAsync(
                async () =>
                {
                    var refreshedDetail = await monitoringWorkflow.GetCampaignDetailAsync(campaignId, cancellationToken);
                    await renderer.WriteCampaignDetailAsync(output, refreshedDetail, timeProvider.GetUtcNow().UtcDateTime);
                    await output.WriteAsync("> ");
                    await output.FlushAsync();
                },
                cancellationToken);

            if (command is null)
            {
                return;
            }

            command = command.Trim();

            if (command.Length == 0 || string.Equals(command, "r", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (command == "0")
            {
                return;
            }

            await output.WriteAsync("Unknown selection. Press Enter to continue.\n> ");
            await output.FlushAsync(cancellationToken);
            await input.ReadLineAsync(cancellationToken);
        }
    }

    private async Task PaintDashboardAsync(bool includeCompletedCampaigns, CancellationToken cancellationToken)
    {
        var refreshedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var dashboard = await monitoringWorkflow.GetDashboardAsync(includeCompletedCampaigns, cancellationToken);
        dashboard = EnrichDashboardWithQueueRates(dashboard, refreshedAtUtc);
        await renderer.WriteDashboardAsync(output, dashboard, includeCompletedCampaigns, refreshedAtUtc);
        await output.WriteAsync("> ");
        await output.FlushAsync(cancellationToken);
    }

    private async Task<string?> ReadCommandWithRefreshAsync(
        Func<Task> repaintAsync,
        CancellationToken cancellationToken)
    {
        if (CanUseInteractiveConsoleInput())
        {
            return await ReadCommandFromInteractiveConsoleAsync(repaintAsync, cancellationToken);
        }

        var readTask = input.ReadLineAsync(cancellationToken).AsTask();

        if (refreshInterval == Timeout.InfiniteTimeSpan)
        {
            return await readTask;
        }

        while (!readTask.IsCompleted)
        {
            await Task.Delay(refreshInterval, cancellationToken);

            if (readTask.IsCompleted)
            {
                break;
            }

            await repaintAsync();
        }

        return await readTask;
    }

    private async Task<string?> ReadCommandFromInteractiveConsoleAsync(
        Func<Task> repaintAsync,
        CancellationToken cancellationToken)
    {
        var buffer = new List<char>();
        var nextRefreshAtUtc = timeProvider.GetUtcNow().UtcDateTime + refreshInterval;

        while (!cancellationToken.IsCancellationRequested)
        {
            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
                    await output.WriteLineAsync();
                    await output.FlushAsync(cancellationToken);
                    return new string([.. buffer]);
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Count == 0)
                    {
                        continue;
                    }

                    buffer.RemoveAt(buffer.Count - 1);
                    await RewritePromptBufferAsync(buffer, cancellationToken);
                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                {
                    buffer.Add(key.KeyChar);
                    await output.WriteAsync(key.KeyChar);
                    await output.FlushAsync(cancellationToken);
                }
            }

            if (refreshInterval != Timeout.InfiniteTimeSpan
                && timeProvider.GetUtcNow().UtcDateTime >= nextRefreshAtUtc)
            {
                await repaintAsync();
                await RewritePromptBufferAsync(buffer, cancellationToken);
                nextRefreshAtUtc = timeProvider.GetUtcNow().UtcDateTime + refreshInterval;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        return null;
    }

    private async Task RewritePromptBufferAsync(
        IReadOnlyList<char> buffer,
        CancellationToken cancellationToken)
    {
        await output.WriteAsync(string.Concat(buffer));
        await output.FlushAsync(cancellationToken);
    }

    private bool CanUseInteractiveConsoleInput()
    {
        return ReferenceEquals(input, Console.In)
            && ReferenceEquals(output, Console.Out)
            && !Console.IsInputRedirected
            && !Console.IsOutputRedirected;
    }

    private MonitoringDashboardSnapshot EnrichDashboardWithQueueRates(
        MonitoringDashboardSnapshot dashboard,
        DateTime refreshedAtUtc)
    {
        if (dashboard.QueueStatus is null)
        {
            return dashboard with
            {
                QueueRates = null,
                TwilioInFlightRates = BuildTwilioInFlightRates(dashboard.Campaigns, refreshedAtUtc)
            };
        }

        queueDepthSamples.Add(new QueueDepthSample(
            refreshedAtUtc,
            dashboard.QueueStatus.VisibleMessages));
        TrimQueueDepthSamples(refreshedAtUtc);

        return dashboard with
        {
            QueueRates = new MonitoringQueueRatesRecord(
                CurrentMessagesPerSecond: ComputeRate(queueDepthSamples, null),
                OneMinuteAverageMessagesPerSecond: ComputeRate(queueDepthSamples, TimeSpan.FromMinutes(1)),
                FiveMinuteAverageMessagesPerSecond: ComputeRate(queueDepthSamples, TimeSpan.FromMinutes(5)),
                TenMinuteAverageMessagesPerSecond: ComputeRate(queueDepthSamples, TimeSpan.FromMinutes(10))),
            TwilioInFlightRates = BuildTwilioInFlightRates(dashboard.Campaigns, refreshedAtUtc)
        };
    }

    private void TrimQueueDepthSamples(DateTime refreshedAtUtc)
    {
        var cutoffUtc = refreshedAtUtc - TenMinuteWindow;
        queueDepthSamples.RemoveAll(sample => sample.CapturedAtUtc < cutoffUtc);
        twilioInFlightSamples.RemoveAll(sample => sample.CapturedAtUtc < cutoffUtc);
    }

    private static double? ComputeRate(
        IReadOnlyList<QueueDepthSample> samples,
        TimeSpan? window)
    {
        if (samples.Count < 2)
        {
            return null;
        }

        var newestSample = samples[^1];
        var oldestSample = window.HasValue
            ? samples.FirstOrDefault(sample => sample.CapturedAtUtc >= newestSample.CapturedAtUtc - window.Value)
            : samples[^2];

        if (oldestSample is null || oldestSample == newestSample)
        {
            return null;
        }

        var elapsedSeconds = (newestSample.CapturedAtUtc - oldestSample.CapturedAtUtc).TotalSeconds;

        if (elapsedSeconds <= 0)
        {
            return null;
        }

        var netDrainRate = (oldestSample.VisibleMessages - newestSample.VisibleMessages) / elapsedSeconds;
        return Math.Max(0, netDrainRate);
    }

    private sealed record QueueDepthSample(
        DateTime CapturedAtUtc,
        int VisibleMessages);

    private MonitoringQueueRatesRecord BuildTwilioInFlightRates(
        IReadOnlyList<CampaignMonitoringSummaryRecord> campaigns,
        DateTime refreshedAtUtc)
    {
        var totalInFlightRecipients = campaigns.Sum(campaign =>
            campaign.SubmittedRecipients + campaign.QueuedRecipients + campaign.SentRecipients);

        twilioInFlightSamples.Add(new QueueDepthSample(
            refreshedAtUtc,
            totalInFlightRecipients));

        return new MonitoringQueueRatesRecord(
            CurrentMessagesPerSecond: ComputeRate(twilioInFlightSamples, null),
            OneMinuteAverageMessagesPerSecond: ComputeRate(twilioInFlightSamples, TimeSpan.FromMinutes(1)),
            FiveMinuteAverageMessagesPerSecond: ComputeRate(twilioInFlightSamples, TimeSpan.FromMinutes(5)),
            TenMinuteAverageMessagesPerSecond: ComputeRate(twilioInFlightSamples, TimeSpan.FromMinutes(10)));
    }
}
