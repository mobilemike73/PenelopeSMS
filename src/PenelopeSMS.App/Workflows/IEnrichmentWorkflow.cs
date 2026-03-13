using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.App.Workflows;

public interface IEnrichmentWorkflow
{
    Task<EnrichmentWorkflowResult> RunAsync(
        CustomerSegment customerSegment = CustomerSegment.Standard,
        bool fullRefresh = false,
        CancellationToken cancellationToken = default);
}

public sealed record EnrichmentWorkflowResult(
    CustomerSegment CustomerSegment,
    bool FullRefresh,
    int SelectedRecords,
    int ProcessedRecords,
    int UpdatedRecords,
    int FailedRecords,
    int SkippedRecords);
