using Microsoft.Extensions.Options;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.App.Options;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;

namespace PenelopeSMS.App.Menu;

public sealed class CampaignMenuAction(
    ICampaignCreationWorkflow campaignCreationWorkflow,
    ICampaignSendWorkflow campaignSendWorkflow,
    IOptions<TwilioOptions> twilioOptions)
{
    private readonly TextReader input = Console.In;
    private readonly TextWriter output = Console.Out;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        output.WriteLine("Campaigns");
        output.WriteLine("1. Create campaign draft");
        output.WriteLine("2. Send next campaign batch");
        output.WriteLine("0. Back");
        output.Write("> ");

        var selection = input.ReadLine();

        if (selection is "0" or null)
        {
            output.WriteLine();
            return;
        }

        if (selection == "2")
        {
            await SendNextBatchAsync(cancellationToken);
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

    private async Task SendNextBatchAsync(CancellationToken cancellationToken)
    {
        var campaigns = await campaignSendWorkflow.ListCampaignsAsync(cancellationToken);
        var sendableCampaigns = campaigns
            .Where(campaign => campaign.PendingRecipients > 0)
            .ToList();

        if (sendableCampaigns.Count == 0)
        {
            output.WriteLine("No drafted campaigns with pending recipients were found.");
            output.WriteLine();
            return;
        }

        output.WriteLine("Drafted campaigns");

        foreach (var campaign in sendableCampaigns)
        {
            output.WriteLine(
                $"[{campaign.CampaignId}] {campaign.CampaignName} | Batch size: {campaign.BatchSize} | Pending: {campaign.PendingRecipients} | Submitted: {campaign.SubmittedRecipients} | Failed: {campaign.FailedRecipients}");
        }

        output.Write("Campaign ID to send: ");
        var rawCampaignId = input.ReadLine();

        if (!int.TryParse(rawCampaignId, out var campaignId) || campaignId <= 0)
        {
            output.WriteLine("Campaign ID must be a positive integer.");
            output.WriteLine();
            return;
        }

        try
        {
            var result = await campaignSendWorkflow.SendNextBatchAsync(
                campaignId,
                cancellationToken);

            output.WriteLine(
                $"Campaign batch sent for {result.CampaignName}. Attempted: {result.AttemptedRecipients}, Accepted: {result.AcceptedRecipients}, Failed: {result.FailedRecipients}, Remaining pending: {result.RemainingPendingRecipients}");

            if (string.IsNullOrWhiteSpace(twilioOptions.Value.StatusCallbackUrl))
            {
                output.WriteLine("Warning: Twilio:StatusCallbackUrl is blank. New sends will not feed the delivery callback pipeline.");
            }
            else
            {
                output.WriteLine($"Delivery callbacks enabled via {twilioOptions.Value.StatusCallbackUrl}. Background processing continues while the app is open.");
            }
        }
        catch (InvalidOperationException exception)
        {
            output.WriteLine(exception.Message);
        }

        output.WriteLine();
    }
}
