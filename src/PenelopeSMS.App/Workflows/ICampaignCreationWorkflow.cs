namespace PenelopeSMS.App.Workflows;

public interface ICampaignCreationWorkflow
{
    Task<CampaignCreationWorkflowResult> CreateDraftAsync(
        string templatePath,
        int batchSize,
        CancellationToken cancellationToken = default);
}
