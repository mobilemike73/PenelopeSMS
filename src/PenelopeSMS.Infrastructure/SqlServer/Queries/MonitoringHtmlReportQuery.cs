using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Infrastructure.SqlServer.Queries;

public sealed class MonitoringHtmlReportQuery(PenelopeSmsDbContext dbContext)
{
    public async Task<MonitoringHtmlReportData> GetReportDataAsync(
        CancellationToken cancellationToken = default)
    {
        var totalPhoneNumbers = await dbContext.PhoneNumberRecords
            .AsNoTracking()
            .CountAsync(cancellationToken);
        var successfulEnrichmentCount = await dbContext.PhoneNumberRecords
            .AsNoTracking()
            .CountAsync(record => record.LastEnrichedAtUtc != null, cancellationToken);
        var failedEnrichmentCount = await dbContext.PhoneNumberRecords
            .AsNoTracking()
            .CountAsync(record => record.EnrichmentFailureStatus != EnrichmentFailureStatus.None, cancellationToken);
        var pendingEnrichmentCount = await dbContext.PhoneNumberRecords
            .AsNoTracking()
            .CountAsync(
                record =>
                    record.LastEnrichedAtUtc == null
                    && record.EnrichmentFailureStatus == EnrichmentFailureStatus.None,
                cancellationToken);
        var eligibleForCampaignsCount = await dbContext.PhoneNumberRecords
            .AsNoTracking()
            .CountAsync(record => record.CampaignEligibilityStatus == CampaignEligibilityStatus.Eligible, cancellationToken);
        var ineligibleForCampaignsCount = await dbContext.PhoneNumberRecords
            .AsNoTracking()
            .CountAsync(record => record.CampaignEligibilityStatus == CampaignEligibilityStatus.Ineligible, cancellationToken);
        var pendingEligibilityCount = await dbContext.PhoneNumberRecords
            .AsNoTracking()
            .CountAsync(record => record.CampaignEligibilityStatus == CampaignEligibilityStatus.Pending, cancellationToken);

        var lineTypeSummaryRows = await dbContext.PhoneNumberRecords
            .AsNoTracking()
            .Where(record => record.TwilioLineType != null)
            .GroupBy(record => record.TwilioLineType!)
            .Select(group => new
            {
                LineType = group.Key,
                RecordCount = group.Count(),
                EligibleCount = group.Count(record => record.CampaignEligibilityStatus == CampaignEligibilityStatus.Eligible),
                IneligibleCount = group.Count(record => record.CampaignEligibilityStatus == CampaignEligibilityStatus.Ineligible),
                PendingCount = group.Count(record => record.CampaignEligibilityStatus == CampaignEligibilityStatus.Pending)
            })
            .OrderByDescending(record => record.RecordCount)
            .ThenBy(record => record.LineType)
            .ToListAsync(cancellationToken);
        var lineTypeSummaries = lineTypeSummaryRows
            .Select(record => new MonitoringLineTypeSummaryRecord(
                record.LineType,
                record.RecordCount,
                record.EligibleCount,
                record.IneligibleCount,
                record.PendingCount))
            .ToList();

        var linkedCarrierGroups = await dbContext.CustomerPhoneLinks
            .AsNoTracking()
            .Where(link => link.PhoneNumberRecord.TwilioLineType != null)
            .GroupBy(link => new
            {
                link.ImportedPhoneSource,
                LineType = link.PhoneNumberRecord.TwilioLineType!,
                link.PhoneNumberRecord.TwilioCarrierName
            })
            .Select(group => new MonitoringCarrierDetailGroupRecord(
                group.Key.ImportedPhoneSource,
                group.Key.LineType,
                group.Key.TwilioCarrierName,
                group.Count()))
            .ToListAsync(cancellationToken);

        var unlinkedCarrierGroups = await dbContext.PhoneNumberRecords
            .AsNoTracking()
            .Where(record =>
                record.TwilioLineType != null
                && !record.CustomerPhoneLinks.Any())
            .GroupBy(record => new
            {
                LineType = record.TwilioLineType!,
                record.TwilioCarrierName
            })
            .Select(group => new MonitoringCarrierDetailGroupRecord(
                null,
                group.Key.LineType,
                group.Key.TwilioCarrierName,
                group.Count()))
            .ToListAsync(cancellationToken);

        var failureStatusRows = await dbContext.PhoneNumberRecords
            .AsNoTracking()
            .Where(record => record.EnrichmentFailureStatus != EnrichmentFailureStatus.None)
            .GroupBy(record => record.EnrichmentFailureStatus)
            .Select(group => new
            {
                FailureStatus = group.Key,
                RecordCount = group.Count(),
                LastFailedAtUtc = group.Max(record => record.LastEnrichmentFailedAtUtc)
            })
            .OrderByDescending(record => record.RecordCount)
            .ThenBy(record => record.FailureStatus)
            .ToListAsync(cancellationToken);
        var failureStatusSummaries = failureStatusRows
            .Select(record => new MonitoringFailureStatusSummaryRecord(
                record.FailureStatus,
                record.RecordCount,
                record.LastFailedAtUtc))
            .ToList();

        var failureCodeRows = await dbContext.PhoneNumberRecords
            .AsNoTracking()
            .Where(record => record.EnrichmentFailureStatus != EnrichmentFailureStatus.None)
            .GroupBy(record => new
            {
                record.EnrichmentFailureStatus,
                ErrorCode = record.LastEnrichmentErrorCode ?? "no-code",
                ErrorMessage = record.LastEnrichmentErrorMessage ?? "No provider detail"
            })
            .Select(group => new
            {
                group.Key.EnrichmentFailureStatus,
                group.Key.ErrorCode,
                group.Key.ErrorMessage,
                RecordCount = group.Count()
            })
            .OrderByDescending(record => record.RecordCount)
            .ThenBy(record => record.ErrorCode)
            .Take(10)
            .ToListAsync(cancellationToken);
        var failureCodeSummaries = failureCodeRows
            .Select(record => new MonitoringFailureCodeSummaryRecord(
                record.EnrichmentFailureStatus,
                record.ErrorCode,
                record.ErrorMessage,
                record.RecordCount))
            .ToList();

        var recentFailures = await dbContext.PhoneNumberRecords
            .AsNoTracking()
            .Where(record => record.EnrichmentFailureStatus != EnrichmentFailureStatus.None)
            .OrderByDescending(record => record.LastEnrichmentFailedAtUtc)
            .ThenBy(record => record.Id)
            .Select(record => new MonitoringRecentFailureRecord(
                record.Id,
                record.CanonicalPhoneNumber,
                record.EnrichmentFailureStatus,
                record.LastEnrichmentFailedAtUtc,
                record.LastEnrichmentErrorCode,
                record.LastEnrichmentErrorMessage,
                record.CampaignEligibilityStatus))
            .Take(25)
            .ToListAsync(cancellationToken);

        var overview = new MonitoringHtmlReportOverview(
            TotalPhoneNumbers: totalPhoneNumbers,
            SuccessfulEnrichmentCount: successfulEnrichmentCount,
            FailedEnrichmentCount: failedEnrichmentCount,
            PendingEnrichmentCount: pendingEnrichmentCount,
            EligibleForCampaignsCount: eligibleForCampaignsCount,
            IneligibleForCampaignsCount: ineligibleForCampaignsCount,
            PendingEligibilityCount: pendingEligibilityCount);

        return new MonitoringHtmlReportData(
            Overview: overview,
            LineTypeSummaries: lineTypeSummaries,
            CarrierRollups: BuildCarrierRollups(linkedCarrierGroups.Concat(unlinkedCarrierGroups)),
            FailureStatusSummaries: failureStatusSummaries,
            FailureCodeSummaries: failureCodeSummaries,
            RecentFailures: recentFailures);
    }

    private static List<MonitoringCarrierRollupRecord> BuildCarrierRollups(
        IEnumerable<MonitoringCarrierDetailGroupRecord> baseGroups)
    {
        var rows = baseGroups.ToList();
        var results = new List<MonitoringCarrierRollupRecord>();

        foreach (var sourceGroup in rows
                     .GroupBy(row => row.ImportedPhoneSource)
                     .OrderBy(group => GetSourceSortKey(group.Key)))
        {
            foreach (var lineTypeGroup in sourceGroup
                         .GroupBy(row => row.LineType, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var carrierGroup in lineTypeGroup
                             .OrderByDescending(row => row.RecordCount)
                             .ThenBy(row => NormalizeCarrier(row.CarrierName), StringComparer.OrdinalIgnoreCase))
                {
                    results.Add(new MonitoringCarrierRollupRecord(
                        SourceLabel: FormatImportedPhoneSource(carrierGroup.ImportedPhoneSource),
                        LineTypeLabel: FormatLineType(carrierGroup.LineType),
                        CarrierLabel: NormalizeCarrier(carrierGroup.CarrierName),
                        RecordCount: carrierGroup.RecordCount,
                        RollupLevel: MonitoringCarrierRollupLevel.Detail));
                }

                results.Add(new MonitoringCarrierRollupRecord(
                    SourceLabel: FormatImportedPhoneSource(sourceGroup.Key),
                    LineTypeLabel: FormatLineType(lineTypeGroup.Key),
                    CarrierLabel: "All carriers",
                    RecordCount: lineTypeGroup.Sum(row => row.RecordCount),
                    RollupLevel: MonitoringCarrierRollupLevel.LineTypeTotal));
            }

            results.Add(new MonitoringCarrierRollupRecord(
                SourceLabel: FormatImportedPhoneSource(sourceGroup.Key),
                LineTypeLabel: "All line types",
                CarrierLabel: "All carriers",
                RecordCount: sourceGroup.Sum(row => row.RecordCount),
                RollupLevel: MonitoringCarrierRollupLevel.SourceTotal));
        }

        results.Add(new MonitoringCarrierRollupRecord(
            SourceLabel: "All sources",
            LineTypeLabel: "All line types",
            CarrierLabel: "All carriers",
            RecordCount: rows.Sum(row => row.RecordCount),
            RollupLevel: MonitoringCarrierRollupLevel.GrandTotal));

        return results;
    }

    private static string FormatImportedPhoneSource(ImportedPhoneSource? importedPhoneSource)
    {
        return importedPhoneSource switch
        {
            ImportedPhoneSource.Phone1 => "Phone 1",
            ImportedPhoneSource.Phone2 => "Phone 2",
            null => "Unlinked",
            _ => importedPhoneSource.ToString() ?? "Unknown"
        };
    }

    private static string FormatLineType(string lineType)
    {
        return string.IsNullOrWhiteSpace(lineType)
            ? "Unknown line type"
            : char.ToUpperInvariant(lineType.Trim()[0]) + lineType.Trim()[1..].ToLowerInvariant();
    }

    private static string NormalizeCarrier(string? carrierName)
    {
        return string.IsNullOrWhiteSpace(carrierName)
            ? "Unknown carrier"
            : carrierName.Trim();
    }

    private static string GetSourceSortKey(ImportedPhoneSource? importedPhoneSource)
    {
        return importedPhoneSource switch
        {
            ImportedPhoneSource.Phone1 => "1",
            ImportedPhoneSource.Phone2 => "2",
            null => "9",
            _ => "8"
        };
    }
}

public sealed record MonitoringHtmlReportData(
    MonitoringHtmlReportOverview Overview,
    IReadOnlyList<MonitoringLineTypeSummaryRecord> LineTypeSummaries,
    IReadOnlyList<MonitoringCarrierRollupRecord> CarrierRollups,
    IReadOnlyList<MonitoringFailureStatusSummaryRecord> FailureStatusSummaries,
    IReadOnlyList<MonitoringFailureCodeSummaryRecord> FailureCodeSummaries,
    IReadOnlyList<MonitoringRecentFailureRecord> RecentFailures);

public sealed record MonitoringHtmlReportOverview(
    int TotalPhoneNumbers,
    int SuccessfulEnrichmentCount,
    int FailedEnrichmentCount,
    int PendingEnrichmentCount,
    int EligibleForCampaignsCount,
    int IneligibleForCampaignsCount,
    int PendingEligibilityCount);

public sealed record MonitoringLineTypeSummaryRecord(
    string LineType,
    int RecordCount,
    int EligibleCount,
    int IneligibleCount,
    int PendingCount);

public sealed record MonitoringCarrierRollupRecord(
    string SourceLabel,
    string LineTypeLabel,
    string CarrierLabel,
    int RecordCount,
    MonitoringCarrierRollupLevel RollupLevel);

public enum MonitoringCarrierRollupLevel
{
    Detail = 0,
    LineTypeTotal = 1,
    SourceTotal = 2,
    GrandTotal = 3
}

public sealed record MonitoringFailureStatusSummaryRecord(
    EnrichmentFailureStatus FailureStatus,
    int RecordCount,
    DateTime? LastFailedAtUtc);

public sealed record MonitoringFailureCodeSummaryRecord(
    EnrichmentFailureStatus FailureStatus,
    string ErrorCode,
    string ErrorMessage,
    int RecordCount);

public sealed record MonitoringRecentFailureRecord(
    int PhoneNumberRecordId,
    string CanonicalPhoneNumber,
    EnrichmentFailureStatus FailureStatus,
    DateTime? LastFailedAtUtc,
    string? ErrorCode,
    string? ErrorMessage,
    CampaignEligibilityStatus EligibilityStatus);

internal sealed record MonitoringCarrierDetailGroupRecord(
    ImportedPhoneSource? ImportedPhoneSource,
    string LineType,
    string? CarrierName,
    int RecordCount);
