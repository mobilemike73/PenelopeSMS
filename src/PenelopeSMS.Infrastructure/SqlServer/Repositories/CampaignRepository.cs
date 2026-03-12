using PenelopeSMS.Domain.Entities;

namespace PenelopeSMS.Infrastructure.SqlServer.Repositories;

public sealed class CampaignRepository(PenelopeSmsDbContext dbContext)
{
    public async Task<CampaignDraftRecord> CreateDraftAsync(
        string name,
        string templateFilePath,
        string templateBody,
        int batchSize,
        IReadOnlyCollection<int> phoneNumberRecordIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateBody);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentNullException.ThrowIfNull(phoneNumberRecordIds);

        var utcNow = DateTime.UtcNow;
        var campaign = new Campaign
        {
            Name = name,
            TemplateFilePath = templateFilePath,
            TemplateBody = templateBody,
            BatchSize = batchSize,
            CreatedAtUtc = utcNow
        };

        foreach (var phoneNumberRecordId in phoneNumberRecordIds.Distinct())
        {
            campaign.Recipients.Add(new CampaignRecipient
            {
                PhoneNumberRecordId = phoneNumberRecordId,
                CreatedAtUtc = utcNow
            });
        }

        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CampaignDraftRecord(
            campaign.Id,
            campaign.Name,
            campaign.BatchSize,
            campaign.Recipients.Count);
    }
}

public sealed record CampaignDraftRecord(
    int CampaignId,
    string CampaignName,
    int BatchSize,
    int RecipientCount);
