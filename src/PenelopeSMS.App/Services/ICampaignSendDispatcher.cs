namespace PenelopeSMS.App.Services;

public interface ICampaignSendDispatcher
{
    ValueTask<CampaignSendDispatchResult> QueueNextBatchAsync(
        int campaignId,
        CancellationToken cancellationToken = default);
}

public sealed record CampaignSendDispatchResult(
    bool WasQueued,
    bool AlreadyQueuedOrRunning);
