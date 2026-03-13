using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.App.Workflows;

public sealed record CampaignCreationWorkflowResult(
    int CampaignId,
    string CampaignName,
    string TemplatePath,
    int BatchSize,
    CustomerSegment AudienceSegment,
    int DraftedRecipients,
    int SkippedIneligibleRecipients);
