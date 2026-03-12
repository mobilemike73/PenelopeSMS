namespace PenelopeSMS.App.Workflows;

public interface IImportWorkflow
{
    Task<ImportWorkflowResult> RunAsync(CancellationToken cancellationToken = default);
}

public sealed record ImportWorkflowResult(
    int ImportBatchId,
    int RowsRead,
    int RowsImported,
    int RowsRejected);
