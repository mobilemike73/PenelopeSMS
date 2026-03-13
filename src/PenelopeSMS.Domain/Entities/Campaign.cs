using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Domain.Entities;

public sealed class Campaign
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string TemplateFilePath { get; set; } = string.Empty;

    public string TemplateBody { get; set; } = string.Empty;

    public int BatchSize { get; set; }

    public CustomerSegment AudienceSegment { get; set; } = CustomerSegment.Standard;

    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<CampaignRecipient> Recipients { get; } = new List<CampaignRecipient>();
}
