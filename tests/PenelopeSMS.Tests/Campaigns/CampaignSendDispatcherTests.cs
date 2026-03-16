using Microsoft.Extensions.DependencyInjection;
using PenelopeSMS.App.Monitoring;
using PenelopeSMS.App.Services;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;

namespace PenelopeSMS.Tests.Campaigns;

public sealed class CampaignSendDispatcherTests
{
    [Fact]
    public async Task QueueNextBatchAsyncDeduplicatesCampaignWhileRunning()
    {
        var workflow = new BlockingCampaignSendWorkflow();
        var services = new ServiceCollection()
            .AddSingleton<IOperationsMonitor, OperationsMonitor>()
            .AddScoped<ICampaignSendWorkflow>(_ => workflow)
            .BuildServiceProvider();

        var dispatcher = new CampaignSendDispatcher(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<IOperationsMonitor>());

        await dispatcher.StartAsync(CancellationToken.None);

        try
        {
            var firstResult = await dispatcher.QueueNextBatchAsync(7);
            await workflow.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));

            var secondResult = await dispatcher.QueueNextBatchAsync(7);

            Assert.True(firstResult.WasQueued);
            Assert.False(firstResult.AlreadyQueuedOrRunning);
            Assert.False(secondResult.WasQueued);
            Assert.True(secondResult.AlreadyQueuedOrRunning);

            workflow.AllowCompletion();
            await workflow.Completed.Task.WaitAsync(TimeSpan.FromSeconds(3));

            var thirdResult = await dispatcher.QueueNextBatchAsync(7);

            Assert.True(thirdResult.WasQueued);
            Assert.False(thirdResult.AlreadyQueuedOrRunning);
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
            await services.DisposeAsync();
        }
    }

    private sealed class BlockingCampaignSendWorkflow : ICampaignSendWorkflow
    {
        private readonly TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> allowCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Started => started;

        public TaskCompletionSource<bool> Completed => completed;

        public Task<IReadOnlyList<CampaignSendSummaryRecord>> ListCampaignsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CampaignSendSummaryRecord>>([]);
        }

        public async Task<CampaignSendWorkflowResult> SendNextBatchAsync(int campaignId, CancellationToken cancellationToken = default)
        {
            started.TrySetResult(true);
            await allowCompletion.Task.WaitAsync(cancellationToken);
            completed.TrySetResult(true);

            return new CampaignSendWorkflowResult(
                CampaignId: campaignId,
                CampaignName: "Test",
                BatchSize: 1,
                AttemptedRecipients: 1,
                AcceptedRecipients: 1,
                FailedRecipients: 0,
                RemainingPendingRecipients: 0);
        }

        public void AllowCompletion()
        {
            allowCompletion.TrySetResult(true);
        }
    }
}
