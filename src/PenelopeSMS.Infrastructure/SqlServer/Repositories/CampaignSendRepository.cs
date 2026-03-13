using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.Twilio;

namespace PenelopeSMS.Infrastructure.SqlServer.Repositories;

public sealed class CampaignSendRepository(PenelopeSmsDbContext dbContext)
{
    public Task<List<CampaignSendSummaryRecord>> ListCampaignsAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Campaigns
            .AsNoTracking()
            .OrderBy(campaign => campaign.Id)
            .Select(campaign => new CampaignSendSummaryRecord(
                campaign.Id,
                campaign.Name,
                campaign.AudienceSegment,
                campaign.BatchSize,
                campaign.Status,
                campaign.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Pending),
                campaign.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Submitted),
                campaign.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Failed)))
            .ToListAsync(cancellationToken);
    }

    public async Task<CampaignSendContext> GetSendContextAsync(
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(campaignId, 1);

        var campaign = await dbContext.Campaigns
            .AsNoTracking()
            .Where(candidate => candidate.Id == campaignId)
            .Select(candidate => new CampaignSendContext(
                candidate.Id,
                candidate.Name,
                candidate.AudienceSegment,
                candidate.TemplateBody,
                candidate.BatchSize,
                candidate.Status))
            .SingleOrDefaultAsync(cancellationToken);

        return campaign
            ?? throw new InvalidOperationException($"Campaign {campaignId} was not found.");
    }

    public async Task ApplySendResultAsync(
        int campaignRecipientId,
        TwilioSendResult sendResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(campaignRecipientId, 1);
        ArgumentNullException.ThrowIfNull(sendResult);

        var campaignRecipient = await dbContext.CampaignRecipients
            .Include(recipient => recipient.Campaign)
            .SingleOrDefaultAsync(recipient => recipient.Id == campaignRecipientId, cancellationToken)
            ?? throw new InvalidOperationException($"Campaign recipient {campaignRecipientId} was not found.");

        var utcNow = DateTime.UtcNow;
        var campaign = campaignRecipient.Campaign;

        campaign.StartedAtUtc ??= utcNow;
        campaign.Status = CampaignStatus.Sending;

        campaignRecipient.LastAttemptedAtUtc = utcNow;
        campaignRecipient.ProviderErrorCode = sendResult.ErrorCode;
        campaignRecipient.ProviderErrorMessage = sendResult.ErrorMessage;

        if (sendResult.IsSuccess)
        {
            campaignRecipient.Status = CampaignRecipientStatus.Submitted;
            campaignRecipient.TwilioMessageSid = sendResult.MessageSid;
            campaignRecipient.InitialTwilioStatus = sendResult.InitialStatus;
            campaignRecipient.SubmittedAtUtc = utcNow;
        }
        else
        {
            campaignRecipient.Status = CampaignRecipientStatus.Failed;
            campaignRecipient.TwilioMessageSid = null;
            campaignRecipient.InitialTwilioStatus = null;
            campaignRecipient.SubmittedAtUtc = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var hasPendingRecipients = await dbContext.CampaignRecipients
            .AnyAsync(
                recipient =>
                    recipient.CampaignId == campaign.Id
                    && recipient.Status == CampaignRecipientStatus.Pending,
                cancellationToken);

        if (!hasPendingRecipients)
        {
            campaign.Status = CampaignStatus.Completed;
            campaign.CompletedAtUtc ??= utcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed record CampaignSendSummaryRecord(
    int CampaignId,
    string CampaignName,
    CustomerSegment AudienceSegment,
    int BatchSize,
    CampaignStatus Status,
    int PendingRecipients,
    int SubmittedRecipients,
    int FailedRecipients);

public sealed record CampaignSendContext(
    int CampaignId,
    string CampaignName,
    CustomerSegment AudienceSegment,
    string TemplateBody,
    int BatchSize,
    CampaignStatus Status);
