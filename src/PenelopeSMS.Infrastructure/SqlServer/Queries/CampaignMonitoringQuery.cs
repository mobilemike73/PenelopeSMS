using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Infrastructure.SqlServer.Queries;

public sealed class CampaignMonitoringQuery(PenelopeSmsDbContext dbContext)
{
    public async Task<IReadOnlyList<CampaignMonitoringSummaryRecord>> ListCampaignsAsync(
        bool includeCompleted = false,
        CancellationToken cancellationToken = default)
    {
        var campaigns = await dbContext.Campaigns
            .AsNoTracking()
            .Select(campaign => new CampaignMonitoringRawRecord(
                campaign.Id,
                campaign.Name,
                campaign.BatchSize,
                campaign.Status,
                campaign.CreatedAtUtc,
                campaign.StartedAtUtc,
                campaign.CompletedAtUtc,
                campaign.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Pending),
                campaign.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Submitted),
                campaign.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Queued),
                campaign.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Sent),
                campaign.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Delivered),
                campaign.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Undelivered),
                campaign.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Failed),
                campaign.Recipients.Max(recipient => (DateTime?)recipient.CreatedAtUtc),
                campaign.Recipients.Max(recipient => recipient.LastAttemptedAtUtc),
                campaign.Recipients.Max(recipient => recipient.SubmittedAtUtc),
                campaign.Recipients.Max(recipient => recipient.CurrentStatusAtUtc),
                campaign.Recipients.Max(recipient => recipient.LastDeliveryCallbackReceivedAtUtc)))
            .ToListAsync(cancellationToken);

        return campaigns
            .Select(MapSummary)
            .Where(campaign => includeCompleted || campaign.Status != CampaignStatus.Completed)
            .OrderByDescending(campaign => campaign.LastActivityAtUtc)
            .ThenByDescending(campaign => campaign.CampaignId)
            .ToList();
    }

    public async Task<CampaignMonitoringDetailRecord?> GetCampaignDetailAsync(
        int campaignId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(campaignId, 1);

        var summary = await dbContext.Campaigns
            .AsNoTracking()
            .Where(candidate => candidate.Id == campaignId)
            .Select(candidate => new CampaignMonitoringRawRecord(
                candidate.Id,
                candidate.Name,
                candidate.BatchSize,
                candidate.Status,
                candidate.CreatedAtUtc,
                candidate.StartedAtUtc,
                candidate.CompletedAtUtc,
                candidate.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Pending),
                candidate.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Submitted),
                candidate.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Queued),
                candidate.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Sent),
                candidate.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Delivered),
                candidate.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Undelivered),
                candidate.Recipients.Count(recipient => recipient.Status == CampaignRecipientStatus.Failed),
                candidate.Recipients.Max(recipient => (DateTime?)recipient.CreatedAtUtc),
                candidate.Recipients.Max(recipient => recipient.LastAttemptedAtUtc),
                candidate.Recipients.Max(recipient => recipient.SubmittedAtUtc),
                candidate.Recipients.Max(recipient => recipient.CurrentStatusAtUtc),
                candidate.Recipients.Max(recipient => recipient.LastDeliveryCallbackReceivedAtUtc)))
            .SingleOrDefaultAsync(cancellationToken);

        if (summary is null)
        {
            return null;
        }

        var recentIssues = await dbContext.CampaignRecipients
            .AsNoTracking()
            .Where(recipient =>
                recipient.CampaignId == campaignId
                && (recipient.ProviderErrorCode != null
                    || recipient.ProviderErrorMessage != null
                    || recipient.DeliveryErrorCode != null
                    || recipient.DeliveryErrorMessage != null
                    || recipient.Status == CampaignRecipientStatus.Undelivered
                    || recipient.Status == CampaignRecipientStatus.Failed))
            .Select(recipient => new CampaignRecipientIssueRecord(
                recipient.Id,
                recipient.PhoneNumberRecord.CanonicalPhoneNumber,
                recipient.Status,
                recipient.TwilioMessageSid,
                recipient.DeliveryErrorCode ?? recipient.ProviderErrorCode,
                recipient.DeliveryErrorMessage ?? recipient.ProviderErrorMessage,
                recipient.CurrentStatusAtUtc
                    ?? recipient.LastDeliveryCallbackReceivedAtUtc
                    ?? recipient.LastAttemptedAtUtc
                    ?? recipient.SubmittedAtUtc
                    ?? recipient.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new CampaignMonitoringDetailRecord(
            MapSummary(summary),
            recentIssues
                .OrderByDescending(issue => issue.OccurredAtUtc)
                .ThenByDescending(issue => issue.CampaignRecipientId)
                .Take(10)
                .ToList());
    }

    private static CampaignMonitoringSummaryRecord MapSummary(CampaignMonitoringRawRecord rawRecord)
    {
        return new CampaignMonitoringSummaryRecord(
            rawRecord.CampaignId,
            rawRecord.CampaignName,
            rawRecord.BatchSize,
            rawRecord.Status,
            rawRecord.PendingRecipients,
            rawRecord.SubmittedRecipients,
            rawRecord.QueuedRecipients,
            rawRecord.SentRecipients,
            rawRecord.DeliveredRecipients,
            rawRecord.UndeliveredRecipients,
            rawRecord.FailedRecipients,
            GetLastActivityAtUtc(rawRecord));
    }

    private static DateTime GetLastActivityAtUtc(CampaignMonitoringRawRecord rawRecord)
    {
        var timestamps = new DateTime?[]
        {
            rawRecord.CompletedAtUtc,
            rawRecord.StartedAtUtc,
            rawRecord.CreatedAtUtc,
            rawRecord.MaxRecipientCreatedAtUtc,
            rawRecord.MaxLastAttemptedAtUtc,
            rawRecord.MaxSubmittedAtUtc,
            rawRecord.MaxCurrentStatusAtUtc,
            rawRecord.MaxLastDeliveryCallbackReceivedAtUtc
        };

        return timestamps.Max() ?? rawRecord.CreatedAtUtc;
    }
}

public sealed record CampaignMonitoringSummaryRecord(
    int CampaignId,
    string CampaignName,
    int BatchSize,
    CampaignStatus Status,
    int PendingRecipients,
    int SubmittedRecipients,
    int QueuedRecipients,
    int SentRecipients,
    int DeliveredRecipients,
    int UndeliveredRecipients,
    int FailedRecipients,
    DateTime LastActivityAtUtc);

public sealed record CampaignMonitoringDetailRecord(
    CampaignMonitoringSummaryRecord Summary,
    IReadOnlyList<CampaignRecipientIssueRecord> RecentIssues);

public sealed record CampaignRecipientIssueRecord(
    int CampaignRecipientId,
    string CanonicalPhoneNumber,
    CampaignRecipientStatus Status,
    string? MessageSid,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime OccurredAtUtc);

internal sealed record CampaignMonitoringRawRecord(
    int CampaignId,
    string CampaignName,
    int BatchSize,
    CampaignStatus Status,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    int PendingRecipients,
    int SubmittedRecipients,
    int QueuedRecipients,
    int SentRecipients,
    int DeliveredRecipients,
    int UndeliveredRecipients,
    int FailedRecipients,
    DateTime? MaxRecipientCreatedAtUtc,
    DateTime? MaxLastAttemptedAtUtc,
    DateTime? MaxSubmittedAtUtc,
    DateTime? MaxCurrentStatusAtUtc,
    DateTime? MaxLastDeliveryCallbackReceivedAtUtc);
