namespace PenelopeSMS.App.Workflows;

public interface IEnrichmentWorkflow
{
    Task<EnrichmentWorkflowResult> RunAsync(
        bool fullRefresh = false,
        CancellationToken cancellationToken = default);
}

public sealed record EnrichmentWorkflowResult(
    bool FullRefresh,
    int SelectedRecords,
    int ProcessedRecords,
    int UpdatedRecords,
    int FailedRecords,
    int SkippedRecords);
