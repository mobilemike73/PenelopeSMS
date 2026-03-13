using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.App.Workflows;

public interface ICampaignCreationWorkflow
{
    Task<CampaignCreationWorkflowResult> CreateDraftAsync(
        string templatePath,
        int batchSize,
        CustomerSegment audienceSegment = CustomerSegment.Standard,
        CancellationToken cancellationToken = default);
}
