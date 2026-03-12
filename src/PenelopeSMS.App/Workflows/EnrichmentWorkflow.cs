using PenelopeSMS.Infrastructure.SqlServer.Queries;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;
using PenelopeSMS.Infrastructure.Twilio;

namespace PenelopeSMS.App.Workflows;

public sealed class EnrichmentWorkflow(
    EnrichmentTargetingQuery enrichmentTargetingQuery,
    PhoneNumberEnrichmentRepository phoneNumberEnrichmentRepository,
    ITwilioLookupClient twilioLookupClient) : IEnrichmentWorkflow
{
    private readonly TextWriter output = Console.Out;

    public async Task<EnrichmentWorkflowResult> RunAsync(
        bool fullRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var totalPhoneNumberCount = await enrichmentTargetingQuery.CountImportedPhoneNumbersAsync(cancellationToken);
        var targets = await enrichmentTargetingQuery.ListTargetsAsync(fullRefresh, cancellationToken);

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
                continue;
            }

            failedRecords++;
            output.WriteLine(
                $"Enrichment failed for {target.CanonicalPhoneNumber}: {lookupResult.ErrorMessage}");
        }

        var skippedRecords = Math.Max(0, totalPhoneNumberCount - targets.Count);

        return new EnrichmentWorkflowResult(
            FullRefresh: fullRefresh,
            SelectedRecords: targets.Count,
            ProcessedRecords: processedRecords,
            UpdatedRecords: updatedRecords,
            FailedRecords: failedRecords,
            SkippedRecords: skippedRecords);
    }
}
