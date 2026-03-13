using PenelopeSMS.App.Monitoring;
using PenelopeSMS.Infrastructure.SqlServer.Queries;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;
using PenelopeSMS.Infrastructure.Twilio;

namespace PenelopeSMS.App.Workflows;

public sealed class CampaignSendWorkflow(
    CampaignSendBatchQuery campaignSendBatchQuery,
    CampaignSendRepository campaignSendRepository,
    ITwilioMessageSender twilioMessageSender,
    IOperationsMonitor? runtimeOperationsMonitor = null,
    TextWriter? runtimeOutput = null) : ICampaignSendWorkflow
{
    private readonly IOperationsMonitor operationsMonitor = runtimeOperationsMonitor ?? NullOperationsMonitor.Instance;
    private readonly TextWriter output = runtimeOutput ?? Console.Out;

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
        var jobId = operationsMonitor.StartJob(
            OperationType.CampaignSend,
            $"Campaign send: {campaign.CampaignName}",
            $"Attempting {batch.Count} recipient(s)");

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
                output.WriteLine(
                    $"Sent to {recipient.CanonicalPhoneNumber}: accepted as {sendResult.InitialStatus ?? "submitted"} (SID: {sendResult.MessageSid ?? "n/a"}).");
            }
            else
            {
                failedRecipients++;
                output.WriteLine(
                    $"Send failed for {recipient.CanonicalPhoneNumber}: {sendResult.ErrorCode ?? "no-code"} | {sendResult.ErrorMessage ?? "Unknown provider error"}");
                operationsMonitor.Warn(
                    OperationType.CampaignSend,
                    $"Campaign send failed for {recipient.CanonicalPhoneNumber}: {sendResult.ErrorMessage ?? sendResult.ErrorCode ?? "Unknown provider error"}",
                    jobId);
            }

            operationsMonitor.UpdateJob(
                jobId,
                $"Processed {acceptedRecipients + failedRecipients}/{batch.Count}, Accepted {acceptedRecipients}, Failed {failedRecipients}");
        }

        var refreshedCampaigns = await campaignSendRepository.ListCampaignsAsync(cancellationToken);
        var refreshedCampaign = refreshedCampaigns.Single(summary => summary.CampaignId == campaignId);
        operationsMonitor.CompleteJob(
            jobId,
            $"Campaign batch sent for {campaign.CampaignName}. Attempted: {batch.Count}, Accepted: {acceptedRecipients}, Failed: {failedRecipients}, Remaining pending: {refreshedCampaign.PendingRecipients}");

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
