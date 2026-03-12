using PenelopeSMS.App.Templates;
using PenelopeSMS.Infrastructure.SqlServer.Queries;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;

namespace PenelopeSMS.App.Workflows;

public sealed class CampaignCreationWorkflow(
    IPlainTextTemplateLoader templateLoader,
    CampaignRecipientSelectionQuery campaignRecipientSelectionQuery,
    CampaignRepository campaignRepository) : ICampaignCreationWorkflow
{
    public async Task<CampaignCreationWorkflowResult> CreateDraftAsync(
        string templatePath,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templatePath);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        var template = await templateLoader.LoadAsync(templatePath, cancellationToken);
        var totalImportedPhoneNumbers = await campaignRecipientSelectionQuery.CountImportedPhoneNumbersAsync(
            cancellationToken);
        var eligibleRecipients = await campaignRecipientSelectionQuery.ListEligibleRecipientsAsync(
            cancellationToken);

        if (eligibleRecipients.Count == 0)
        {
            throw new InvalidOperationException("No eligible phone numbers are available for campaign creation.");
        }

        var normalizedTemplatePath = template.TemplatePath.Replace('\\', '/');
        var campaignName = Path.GetFileNameWithoutExtension(normalizedTemplatePath);
        var draft = await campaignRepository.CreateDraftAsync(
            name: campaignName,
            templateFilePath: template.TemplatePath,
            templateBody: template.TemplateBody,
            batchSize: batchSize,
            phoneNumberRecordIds: eligibleRecipients
                .Select(recipient => recipient.PhoneNumberRecordId)
                .ToArray(),
            cancellationToken: cancellationToken);

        return new CampaignCreationWorkflowResult(
            CampaignId: draft.CampaignId,
            CampaignName: draft.CampaignName,
            TemplatePath: template.TemplatePath,
            BatchSize: draft.BatchSize,
            DraftedRecipients: draft.RecipientCount,
            SkippedIneligibleRecipients: Math.Max(0, totalImportedPhoneNumbers - draft.RecipientCount));
    }
}
