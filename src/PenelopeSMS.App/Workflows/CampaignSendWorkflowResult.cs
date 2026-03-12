namespace PenelopeSMS.App.Workflows;

public sealed record CampaignSendWorkflowResult(
    int CampaignId,
    string CampaignName,
    int BatchSize,
    int AttemptedRecipients,
    int AcceptedRecipients,
    int FailedRecipients,
    int RemainingPendingRecipients);
