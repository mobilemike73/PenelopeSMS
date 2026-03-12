using PenelopeSMS.App.Workflows;

namespace PenelopeSMS.App.Menu;

public sealed class EnrichmentFailureMenuAction(IEnrichmentRetryWorkflow enrichmentRetryWorkflow)
{
    private readonly TextReader input = Console.In;
    private readonly TextWriter output = Console.Out;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var failures = await enrichmentRetryWorkflow.ReviewFailuresAsync(cancellationToken);

        if (failures.Count == 0)
        {
            output.WriteLine("No failed enrichments found.");
            output.WriteLine();
            return;
        }

        output.WriteLine("Failed enrichments");

        foreach (var failure in failures)
        {
            output.WriteLine(
                $"[{failure.PhoneNumberRecordId}] {failure.CanonicalPhoneNumber} | {(failure.IsRetryable ? "Retryable" : "Permanent")} | Eligibility: {failure.EligibilityStatus} | {failure.ErrorCode ?? "no-code"} | {failure.ErrorMessage ?? "No error message"}");
        }

        output.WriteLine("1. Retry all retryable failures");
        output.WriteLine("2. Retry selected failures");
        output.WriteLine("0. Back");
        output.Write("> ");

        var selection = input.ReadLine();

        if (selection is "0" or null)
        {
            output.WriteLine();
            return;
        }

        EnrichmentRetryWorkflowResult result;

        switch (selection)
        {
            case "1":
                result = await enrichmentRetryWorkflow.RetryAllAsync(cancellationToken);
                break;
            case "2":
                output.Write("Enter phone record IDs separated by commas: ");
                var rawIds = input.ReadLine();
                var selectedIds = ParseIds(rawIds);

                if (selectedIds.Length == 0)
                {
                    output.WriteLine("No valid IDs were provided.");
                    output.WriteLine();
                    return;
                }

                result = await enrichmentRetryWorkflow.RetrySelectedAsync(
                    selectedIds,
                    cancellationToken);
                break;
            default:
                output.WriteLine("Unknown selection.");
                output.WriteLine();
                return;
        }

        output.WriteLine(
            $"Retry complete. Requested: {result.RequestedRecords}, Processed: {result.ProcessedRecords}, Updated: {result.UpdatedRecords}, Failed: {result.FailedRecords}, Skipped: {result.SkippedRecords}");
        output.WriteLine();
    }

    private static int[] ParseIds(string? rawIds)
    {
        if (string.IsNullOrWhiteSpace(rawIds))
        {
            return Array.Empty<int>();
        }

        return rawIds
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value, out var parsed) ? parsed : (int?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Distinct()
            .ToArray();
    }
}
