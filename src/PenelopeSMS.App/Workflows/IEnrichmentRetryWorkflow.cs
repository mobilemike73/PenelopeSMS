using PenelopeSMS.Infrastructure.SqlServer.Queries;

namespace PenelopeSMS.App.Workflows;

public interface IEnrichmentRetryWorkflow
{
    Task<IReadOnlyList<FailedEnrichmentReviewRecord>> ReviewFailuresAsync(
        CancellationToken cancellationToken = default);

    Task<EnrichmentRetryWorkflowResult> RetryAllAsync(
        CancellationToken cancellationToken = default);

    Task<EnrichmentRetryWorkflowResult> RetrySelectedAsync(
        IReadOnlyCollection<int> phoneNumberRecordIds,
        CancellationToken cancellationToken = default);
}

public sealed record EnrichmentRetryWorkflowResult(
    int RequestedRecords,
    int ProcessedRecords,
    int UpdatedRecords,
    int FailedRecords,
    int SkippedRecords);
