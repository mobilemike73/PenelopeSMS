SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @UtcNow datetime2(7) = SYSUTCDATETIME();

;WITH VipPhoneRecords AS
(
    SELECT DISTINCT
        link.PhoneNumberRecordId
    FROM dbo.CustomerPhoneLinks AS link
    WHERE link.IsVip = 1
)
UPDATE record
SET
    record.LastImportedAtUtc = @UtcNow,
    record.LastEnrichmentAttemptedAtUtc = @UtcNow,
    record.LastEnrichedAtUtc = @UtcNow,
    record.CampaignEligibilityStatus = 1, -- Eligible
    record.EligibilityEvaluatedAtUtc = @UtcNow,
    record.EnrichmentFailureStatus = 0,   -- None
    record.LastEnrichmentFailedAtUtc = NULL,
    record.LastEnrichmentErrorCode = NULL,
    record.LastEnrichmentErrorMessage = NULL
FROM dbo.PhoneNumberRecords AS record
INNER JOIN VipPhoneRecords AS vip
    ON vip.PhoneNumberRecordId = record.Id;

SELECT
    COUNT(*) AS VipPhoneRecordsEnriched
FROM dbo.PhoneNumberRecords AS record
WHERE EXISTS
(
    SELECT 1
    FROM dbo.CustomerPhoneLinks AS link
    WHERE link.PhoneNumberRecordId = record.Id
      AND link.IsVip = 1
)
AND record.CampaignEligibilityStatus = 1
AND record.LastEnrichedAtUtc IS NOT NULL;
