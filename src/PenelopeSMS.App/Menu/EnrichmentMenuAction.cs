using PenelopeSMS.App.Workflows;

using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.App.Menu;

public sealed class EnrichmentMenuAction(
    IEnrichmentWorkflow enrichmentWorkflow,
    EnrichmentFailureMenuAction enrichmentFailureMenuAction)
{
    private readonly TextReader input = Console.In;
    private readonly TextWriter output = Console.Out;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        output.WriteLine("Run phone enrichment");
        output.WriteLine("1. Standard due-record refresh");
        output.WriteLine("2. Standard full refresh");
        output.WriteLine("3. VIP due-record refresh");
        output.WriteLine("4. VIP full refresh");
        output.WriteLine("5. Review failed enrichments");
        output.WriteLine("0. Cancel");
        output.Write("> ");

        var selection = input.ReadLine();

        if (selection is "0" or null)
        {
            output.WriteLine();
            return;
        }

        if (selection == "5")
        {
            await enrichmentFailureMenuAction.ExecuteAsync(cancellationToken);
            return;
        }

        if (selection is not ("1" or "2" or "3" or "4"))
        {
            output.WriteLine("Unknown selection.");
            output.WriteLine();
            return;
        }

        var customerSegment = selection is "3" or "4"
            ? CustomerSegment.Vip
            : CustomerSegment.Standard;
        var fullRefresh = selection is "2" or "4";
        var segmentLabel = customerSegment == CustomerSegment.Vip ? "VIP" : "standard";

        output.WriteLine(
            fullRefresh
                ? $"Starting {segmentLabel} full enrichment refresh..."
                : $"Starting {segmentLabel} due-record enrichment...");

        var result = await enrichmentWorkflow.RunAsync(customerSegment, fullRefresh, cancellationToken);

        output.WriteLine(
            $"{FormatCustomerSegment(result.CustomerSegment)} enrichment complete. Selected: {result.SelectedRecords}, Processed: {result.ProcessedRecords}, Updated: {result.UpdatedRecords}, Failed: {result.FailedRecords}, Skipped: {result.SkippedRecords}");
        output.WriteLine();
    }

    private static string FormatCustomerSegment(CustomerSegment customerSegment)
    {
        return customerSegment == CustomerSegment.Vip ? "VIP" : "Standard";
    }
}
