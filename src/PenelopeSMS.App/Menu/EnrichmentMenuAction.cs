using PenelopeSMS.App.Workflows;

namespace PenelopeSMS.App.Menu;

public sealed class EnrichmentMenuAction(IEnrichmentWorkflow enrichmentWorkflow)
{
    private readonly TextReader input = Console.In;
    private readonly TextWriter output = Console.Out;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        output.WriteLine("Run phone enrichment");
        output.WriteLine("1. Default due-record refresh");
        output.WriteLine("2. Full refresh");
        output.WriteLine("0. Cancel");
        output.Write("> ");

        var selection = input.ReadLine();

        if (selection is "0" or null)
        {
            output.WriteLine();
            return;
        }

        if (selection is not ("1" or "2"))
        {
            output.WriteLine("Unknown selection.");
            output.WriteLine();
            return;
        }

        var fullRefresh = selection == "2";

        output.WriteLine(
            fullRefresh
                ? "Starting full enrichment refresh..."
                : "Starting due-record enrichment...");

        var result = await enrichmentWorkflow.RunAsync(fullRefresh, cancellationToken);

        output.WriteLine(
            $"Enrichment complete. Selected: {result.SelectedRecords}, Processed: {result.ProcessedRecords}, Updated: {result.UpdatedRecords}, Failed: {result.FailedRecords}, Skipped: {result.SkippedRecords}");
        output.WriteLine();
    }
}
