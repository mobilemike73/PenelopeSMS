using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PenelopeSMS.App.Monitoring;
using PenelopeSMS.App.Workflows;

namespace PenelopeSMS.App.Services;

public sealed class CampaignSendDispatcher(
    IServiceScopeFactory serviceScopeFactory,
    IOperationsMonitor operationsMonitor) : BackgroundService, ICampaignSendDispatcher
{
    private readonly Channel<int> campaignQueue = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });
    private readonly ConcurrentDictionary<int, byte> queuedOrRunningCampaignIds = [];

    public async ValueTask<CampaignSendDispatchResult> QueueNextBatchAsync(
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(campaignId, 1);

        if (!queuedOrRunningCampaignIds.TryAdd(campaignId, 0))
        {
            return new CampaignSendDispatchResult(
                WasQueued: false,
                AlreadyQueuedOrRunning: true);
        }

        try
        {
            await campaignQueue.Writer.WriteAsync(campaignId, cancellationToken);
            return new CampaignSendDispatchResult(
                WasQueued: true,
                AlreadyQueuedOrRunning: false);
        }
        catch
        {
            queuedOrRunningCampaignIds.TryRemove(campaignId, out _);
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var campaignId in campaignQueue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var workflow = scope.ServiceProvider.GetRequiredService<ICampaignSendWorkflow>();
                await workflow.SendNextBatchAsync(campaignId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                operationsMonitor.Warn(
                    OperationType.CampaignSend,
                    $"Background campaign send failed for campaign {campaignId}: {exception.Message}");
            }
            finally
            {
                queuedOrRunningCampaignIds.TryRemove(campaignId, out _);
            }
        }
    }
}
