using PenelopeSMS.Infrastructure.SqlServer.Repositories;

namespace PenelopeSMS.App.Workflows;

public interface ICampaignSendWorkflow
{
    Task<IReadOnlyList<CampaignSendSummaryRecord>> ListCampaignsAsync(
        CancellationToken cancellationToken = default);

    Task<CampaignSendWorkflowResult> SendNextBatchAsync(
        int campaignId,
        CancellationToken cancellationToken = default);
}
