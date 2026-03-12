using PenelopeSMS.App.Workflows;

namespace PenelopeSMS.App.Menu;

public sealed class ImportMenuAction(IImportWorkflow importWorkflow)
{
    private readonly TextWriter output = Console.Out;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        output.WriteLine("Starting Oracle import...");

        var result = await importWorkflow.RunAsync(cancellationToken);

        output.WriteLine(
            $"Import batch {result.ImportBatchId} complete. Read: {result.RowsRead}, Imported: {result.RowsImported}, Rejected: {result.RowsRejected}");
        output.WriteLine();
    }
}
