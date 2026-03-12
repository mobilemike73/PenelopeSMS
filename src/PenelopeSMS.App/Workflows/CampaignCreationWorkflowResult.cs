namespace PenelopeSMS.App.Workflows;

public sealed record CampaignCreationWorkflowResult(
    int CampaignId,
    string CampaignName,
    string TemplatePath,
    int BatchSize,
    int DraftedRecipients,
    int SkippedIneligibleRecipients);
