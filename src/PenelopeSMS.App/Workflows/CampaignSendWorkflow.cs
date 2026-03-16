using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
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
    TextWriter? runtimeOutput = null,
    IServiceScopeFactory? serviceScopeFactory = null) : ICampaignSendWorkflow
{
    private const int MaxParallelSends = 16;
    private readonly IOperationsMonitor operationsMonitor = runtimeOperationsMonitor ?? NullOperationsMonitor.Instance;
    private readonly TextWriter output = runtimeOutput ?? TextWriter.Null;

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

        if (serviceScopeFactory is null)
        {
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
        }
        else
        {
            var outcomeMessages = new ConcurrentBag<CampaignSendOutcomeMessage>();
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(MaxParallelSends, batch.Count == 0 ? 1 : batch.Count),
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(
                batch.Select((recipient, index) => new IndexedCampaignRecipient(index, recipient)),
                parallelOptions,
                async (item, token) =>
                {
                    await using var scope = serviceScopeFactory.CreateAsyncScope();
                    var scopedSender = scope.ServiceProvider.GetRequiredService<ITwilioMessageSender>();
                    var scopedRepository = scope.ServiceProvider.GetRequiredService<CampaignSendRepository>();
                    var sendResult = await scopedSender.SendAsync(
                        item.Recipient.CanonicalPhoneNumber,
                        campaign.TemplateBody,
                        token);

                    await scopedRepository.ApplySendResultAsync(
                        item.Recipient.CampaignRecipientId,
                        sendResult,
                        token);

                    var outputMessage = sendResult.IsSuccess
                        ? $"Sent to {item.Recipient.CanonicalPhoneNumber}: accepted as {sendResult.InitialStatus ?? "submitted"} (SID: {sendResult.MessageSid ?? "n/a"})."
                        : $"Send failed for {item.Recipient.CanonicalPhoneNumber}: {sendResult.ErrorCode ?? "no-code"} | {sendResult.ErrorMessage ?? "Unknown provider error"}";
                    var warningMessage = sendResult.IsSuccess
                        ? null
                        : $"Campaign send failed for {item.Recipient.CanonicalPhoneNumber}: {sendResult.ErrorMessage ?? sendResult.ErrorCode ?? "Unknown provider error"}";

                    outcomeMessages.Add(new CampaignSendOutcomeMessage(item.Index, outputMessage, warningMessage));

                    if (sendResult.IsSuccess)
                    {
                        var accepted = Interlocked.Increment(ref acceptedRecipients);
                        var processedCount = accepted + Volatile.Read(ref failedRecipients);
                        operationsMonitor.UpdateJob(
                            jobId,
                            $"Processed {processedCount}/{batch.Count}, Accepted {accepted}, Failed {Volatile.Read(ref failedRecipients)}");
                        return;
                    }

                    {
                        var failed = Interlocked.Increment(ref failedRecipients);
                        var processedCount = Volatile.Read(ref acceptedRecipients) + failed;
                        operationsMonitor.UpdateJob(
                            jobId,
                            $"Processed {processedCount}/{batch.Count}, Accepted {Volatile.Read(ref acceptedRecipients)}, Failed {Volatile.Read(ref failedRecipients)}");
                    }
                });

            foreach (var outcome in outcomeMessages.OrderBy(message => message.Index))
            {
                output.WriteLine(outcome.OutputMessage);

                if (outcome.WarningMessage is not null)
                {
                    operationsMonitor.Warn(OperationType.CampaignSend, outcome.WarningMessage, jobId);
                }
            }
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

    private sealed record IndexedCampaignRecipient(int Index, CampaignSendBatchRecord Recipient);

    private sealed record CampaignSendOutcomeMessage(
        int Index,
        string OutputMessage,
        string? WarningMessage);
}
