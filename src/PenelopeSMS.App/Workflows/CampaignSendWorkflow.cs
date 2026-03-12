using PenelopeSMS.Infrastructure.SqlServer.Queries;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;
using PenelopeSMS.Infrastructure.Twilio;

namespace PenelopeSMS.App.Workflows;

public sealed class CampaignSendWorkflow(
    CampaignSendBatchQuery campaignSendBatchQuery,
    CampaignSendRepository campaignSendRepository,
    ITwilioMessageSender twilioMessageSender) : ICampaignSendWorkflow
{
    public async Task<IReadOnlyList<CampaignSendSummaryRecord>> ListCampaignsAsync(
        CancellationToken cancellationToken = default)
    {
        return await campaignSendRepository.ListCampaignsAsync(cancellationToken);
    }

    public async Task<CampaignSendWorkflowResult> SendNextBatchAsync(
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(campaignId, 1);

        var campaign = await campaignSendRepository.GetSendContextAsync(campaignId, cancellationToken);
        var batch = await campaignSendBatchQuery.ListPendingBatchAsync(
            campaignId,
            campaign.BatchSize,
            cancellationToken);

        var acceptedRecipients = 0;
        var failedRecipients = 0;

        foreach (var recipient in batch)
        {
            var sendResult = await twilioMessageSender.SendAsync(
                recipient.CanonicalPhoneNumber,
                campaign.TemplateBody,
                cancellationToken);

            await campaignSendRepository.ApplySendResultAsync(
                recipient.CampaignRecipientId,
                sendResult,
                cancellationToken);

            if (sendResult.IsSuccess)
            {
                acceptedRecipients++;
            }
            else
            {
                failedRecipients++;
            }
        }

        var refreshedCampaigns = await campaignSendRepository.ListCampaignsAsync(cancellationToken);
        var refreshedCampaign = refreshedCampaigns.Single(summary => summary.CampaignId == campaignId);

        return new CampaignSendWorkflowResult(
            CampaignId: campaign.CampaignId,
            CampaignName: campaign.CampaignName,
            BatchSize: campaign.BatchSize,
            AttemptedRecipients: batch.Count,
            AcceptedRecipients: acceptedRecipients,
            FailedRecipients: failedRecipients,
            RemainingPendingRecipients: refreshedCampaign.PendingRecipients);
    }
}
