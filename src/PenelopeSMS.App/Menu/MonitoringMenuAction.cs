using PenelopeSMS.App.Rendering;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Infrastructure.SqlServer.Queries;

namespace PenelopeSMS.App.Menu;

public sealed class MonitoringMenuAction
{
    private readonly IMonitoringWorkflow monitoringWorkflow;
    private readonly MonitoringScreenRenderer renderer;
    private readonly TextReader input;
    private readonly TextWriter output;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan refreshInterval;

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
        var dashboard = await monitoringWorkflow.GetDashboardAsync(includeCompletedCampaigns, cancellationToken);
        await renderer.WriteDashboardAsync(output, dashboard, includeCompletedCampaigns, timeProvider.GetUtcNow().UtcDateTime);
        await output.WriteAsync("> ");
        await output.FlushAsync(cancellationToken);
    }

    private async Task<string?> ReadCommandWithRefreshAsync(
        Func<Task> repaintAsync,
        CancellationToken cancellationToken)
    {
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
}
