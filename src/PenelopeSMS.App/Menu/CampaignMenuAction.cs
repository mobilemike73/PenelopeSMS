using PenelopeSMS.App.Workflows;

namespace PenelopeSMS.App.Menu;

public sealed class CampaignMenuAction(ICampaignCreationWorkflow campaignCreationWorkflow)
{
    private readonly TextReader input = Console.In;
    private readonly TextWriter output = Console.Out;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        output.WriteLine("Campaigns");
        output.WriteLine("1. Create campaign draft");
        output.WriteLine("0. Back");
        output.Write("> ");

        var selection = input.ReadLine();

        if (selection is "0" or null)
        {
            output.WriteLine();
            return;
        }

        if (selection != "1")
        {
            output.WriteLine("Unknown selection.");
            output.WriteLine();
            return;
        }

        output.Write("Template file path: ");
        var templatePath = input.ReadLine();

        if (string.IsNullOrWhiteSpace(templatePath))
        {
            output.WriteLine("Template file path is required.");
            output.WriteLine();
            return;
        }

        output.Write("Batch size: ");
        var rawBatchSize = input.ReadLine();

        if (!int.TryParse(rawBatchSize, out var batchSize) || batchSize <= 0)
        {
            output.WriteLine("Batch size must be a positive integer.");
            output.WriteLine();
            return;
        }

        try
        {
            var result = await campaignCreationWorkflow.CreateDraftAsync(
                templatePath,
                batchSize,
                cancellationToken);

            output.WriteLine(
                $"Campaign draft {result.CampaignId} ({result.CampaignName}) created. Drafted: {result.DraftedRecipients}, Skipped: {result.SkippedIneligibleRecipients}, Batch size: {result.BatchSize}");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            output.WriteLine(exception.Message);
        }

        output.WriteLine();
    }
}
