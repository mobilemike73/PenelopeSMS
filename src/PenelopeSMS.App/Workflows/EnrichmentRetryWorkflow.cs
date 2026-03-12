using PenelopeSMS.Infrastructure.SqlServer.Queries;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;
using PenelopeSMS.Infrastructure.Twilio;

namespace PenelopeSMS.App.Workflows;

public sealed class EnrichmentRetryWorkflow(
    FailedEnrichmentReviewQuery failedEnrichmentReviewQuery,
    PhoneNumberEnrichmentRepository phoneNumberEnrichmentRepository,
    ITwilioLookupClient twilioLookupClient) : IEnrichmentRetryWorkflow
{
    private readonly TextWriter output = Console.Out;

    public async Task<IReadOnlyList<FailedEnrichmentReviewRecord>> ReviewFailuresAsync(
        CancellationToken cancellationToken = default)
    {
        return await failedEnrichmentReviewQuery.ListFailuresAsync(cancellationToken);
    }

    public async Task<EnrichmentRetryWorkflowResult> RetryAllAsync(
        CancellationToken cancellationToken = default)
    {
        var failures = await failedEnrichmentReviewQuery.ListFailuresAsync(cancellationToken);
        var retryableTargets = await failedEnrichmentReviewQuery.ListRetryableTargetsAsync(cancellationToken);

        return await RetryAsync(
            requestedRecords: failures.Count,
            targets: retryableTargets,
            cancellationToken);
    }

    public async Task<EnrichmentRetryWorkflowResult> RetrySelectedAsync(
        IReadOnlyCollection<int> phoneNumberRecordIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(phoneNumberRecordIds);

        var distinctIds = phoneNumberRecordIds.Distinct().ToArray();
        var retryableTargets = await failedEnrichmentReviewQuery.ListRetryableTargetsByIdsAsync(
            distinctIds,
            cancellationToken);

        return await RetryAsync(
            requestedRecords: distinctIds.Length,
            targets: retryableTargets,
            cancellationToken);
    }

    private async Task<EnrichmentRetryWorkflowResult> RetryAsync(
        int requestedRecords,
        List<EnrichmentTargetRecord> targets,
        CancellationToken cancellationToken)
    {
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
                $"Retry failed for {target.CanonicalPhoneNumber}: {lookupResult.ErrorMessage}");
        }

        return new EnrichmentRetryWorkflowResult(
            RequestedRecords: requestedRecords,
            ProcessedRecords: processedRecords,
            UpdatedRecords: updatedRecords,
            FailedRecords: failedRecords,
            SkippedRecords: Math.Max(0, requestedRecords - targets.Count));
    }
}
