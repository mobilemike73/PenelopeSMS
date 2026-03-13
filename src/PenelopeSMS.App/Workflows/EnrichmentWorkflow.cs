using PenelopeSMS.App.Monitoring;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.SqlServer.Queries;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;
using PenelopeSMS.Infrastructure.Twilio;

namespace PenelopeSMS.App.Workflows;

public sealed class EnrichmentWorkflow(
    EnrichmentTargetingQuery enrichmentTargetingQuery,
    PhoneNumberEnrichmentRepository phoneNumberEnrichmentRepository,
    ITwilioLookupClient twilioLookupClient,
    IOperationsMonitor? runtimeOperationsMonitor = null) : IEnrichmentWorkflow
{
    private readonly IOperationsMonitor operationsMonitor = runtimeOperationsMonitor ?? NullOperationsMonitor.Instance;
    private readonly TextWriter output = Console.Out;

    public async Task<EnrichmentWorkflowResult> RunAsync(
        CustomerSegment customerSegment = CustomerSegment.Standard,
        bool fullRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var segmentLabel = customerSegment == CustomerSegment.Vip ? "VIP" : "Standard";
        var label = fullRefresh
            ? $"{segmentLabel} full enrichment refresh"
            : $"{segmentLabel} due-record enrichment";
        var jobId = operationsMonitor.StartJob(OperationType.Enrichment, label, "Selecting records");
        var totalPhoneNumberCount = await enrichmentTargetingQuery.CountImportedPhoneNumbersAsync(customerSegment, cancellationToken);
        var targets = await enrichmentTargetingQuery.ListTargetsAsync(customerSegment, fullRefresh, cancellationToken);

        var processedRecords = 0;
        var updatedRecords = 0;
        var failedRecords = 0;

        foreach (var target in targets)
        {
            var lookupResult = await twilioLookupClient.LookupAsync(
                target.CanonicalPhoneNumber,
                cancellationToken);

            await phoneNumberEnrichmentRepository.ApplyResultAsync(
                target.PhoneNumberRecordId,
                lookupResult,
                cancellationToken);

            processedRecords++;

            if (lookupResult.IsSuccess)
            {
                updatedRecords++;
                operationsMonitor.UpdateJob(
                    jobId,
                    $"Processed {processedRecords}/{targets.Count}, Updated {updatedRecords}, Failed {failedRecords}");
                continue;
            }

            failedRecords++;
            output.WriteLine(
                $"Enrichment failed for {target.CanonicalPhoneNumber}: {lookupResult.ErrorMessage}");
            operationsMonitor.Warn(
                OperationType.Enrichment,
                $"Enrichment failed for {target.CanonicalPhoneNumber}: {lookupResult.ErrorMessage}",
                jobId);
            operationsMonitor.UpdateJob(
                jobId,
                $"Processed {processedRecords}/{targets.Count}, Updated {updatedRecords}, Failed {failedRecords}");
        }

        var skippedRecords = Math.Max(0, totalPhoneNumberCount - targets.Count);
        operationsMonitor.CompleteJob(
            jobId,
            $"Enrichment complete. Selected: {targets.Count}, Processed: {processedRecords}, Updated: {updatedRecords}, Failed: {failedRecords}, Skipped: {skippedRecords}");

        return new EnrichmentWorkflowResult(
            CustomerSegment: customerSegment,
            FullRefresh: fullRefresh,
            SelectedRecords: targets.Count,
            ProcessedRecords: processedRecords,
            UpdatedRecords: updatedRecords,
            FailedRecords: failedRecords,
            SkippedRecords: skippedRecords);
    }
}
