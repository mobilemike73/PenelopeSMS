SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @RawPhoneNumber nvarchar(64) = '517-306-3380';
DECLARE @CanonicalPhoneNumber nvarchar(32) = '+15173063380';
DECLARE @CustSid nvarchar(64) = 'TEST-VIP-5173063380';
DECLARE @ImportedPhoneSource int = 1; -- PHONE1
DECLARE @IsVip bit = 1;
DECLARE @UtcNow datetime2(7) = SYSUTCDATETIME();
DECLARE @ImportBatchId int;
DECLARE @RowsImported int = 0;

DELETE FROM dbo.CampaignRecipientStatusHistory;
DELETE FROM dbo.CampaignRecipients;
DELETE FROM dbo.Campaigns;
DELETE FROM dbo.CustomerPhoneLinks;
DELETE FROM dbo.PhoneNumberRecords;
DELETE FROM dbo.ImportBatches;

DBCC CHECKIDENT ('CampaignRecipientStatusHistory', RESEED, 1);
DBCC CHECKIDENT ('CampaignRecipients', RESEED, 1);
DBCC CHECKIDENT ('Campaigns', RESEED, 1);
DBCC CHECKIDENT ('CustomerPhoneLinks', RESEED, 1);
DBCC CHECKIDENT ('PhoneNumberRecords', RESEED, 1);
DBCC CHECKIDENT ('ImportBatches', RESEED, 1);

CREATE TABLE #ImportPhoneRows
(
    CustSid nvarchar(64) NOT NULL,
    RawPhoneNumber nvarchar(64) NOT NULL,
    CanonicalPhoneNumber nvarchar(32) NOT NULL,
    IsVip bit NOT NULL,
    ImportedPhoneSource int NOT NULL
);

INSERT INTO #ImportPhoneRows
(
    CustSid,
    RawPhoneNumber,
    CanonicalPhoneNumber,
    IsVip,
    ImportedPhoneSource
)
VALUES
(
    @CustSid,
    @RawPhoneNumber,
    @CanonicalPhoneNumber,
    @IsVip,
    @ImportedPhoneSource
);

INSERT INTO dbo.ImportBatches
(
    StartedAtUtc,
    CompletedAtUtc,
    RowsRead,
    RowsImported,
    RowsRejected,
    Status
)
VALUES
(
    @UtcNow,
    NULL,
    0,
    0,
    0,
    'InProgress'
);

SET @ImportBatchId = CAST(SCOPE_IDENTITY() AS int);

;WITH DistinctPhones AS
(
    SELECT DISTINCT
        stage.CanonicalPhoneNumber
    FROM #ImportPhoneRows AS stage
)
INSERT INTO dbo.PhoneNumberRecords
(
    CanonicalPhoneNumber,
    CreatedAtUtc,
    LastImportedAtUtc,
    LastEnrichmentAttemptedAtUtc,
    LastEnrichedAtUtc,
    CampaignEligibilityStatus,
    EligibilityEvaluatedAtUtc,
    EnrichmentFailureStatus
)
SELECT
    source.CanonicalPhoneNumber,
    @UtcNow,
    @UtcNow,
    @UtcNow,
    @UtcNow,
    1, -- Eligible
    @UtcNow,
    0  -- None
FROM DistinctPhones AS source
LEFT JOIN dbo.PhoneNumberRecords AS target
    ON target.CanonicalPhoneNumber = source.CanonicalPhoneNumber
WHERE target.Id IS NULL;

;WITH DistinctPhones AS
(
    SELECT DISTINCT
        stage.CanonicalPhoneNumber
    FROM #ImportPhoneRows AS stage
)
UPDATE target
SET
    target.LastImportedAtUtc = @UtcNow,
    target.LastEnrichmentAttemptedAtUtc = @UtcNow,
    target.LastEnrichedAtUtc = @UtcNow,
    target.CampaignEligibilityStatus = 1,
    target.EligibilityEvaluatedAtUtc = @UtcNow,
    target.EnrichmentFailureStatus = 0,
    target.LastEnrichmentFailedAtUtc = NULL,
    target.LastEnrichmentErrorCode = NULL,
    target.LastEnrichmentErrorMessage = NULL
FROM dbo.PhoneNumberRecords AS target
INNER JOIN DistinctPhones AS source
    ON source.CanonicalPhoneNumber = target.CanonicalPhoneNumber;

;WITH ResolvedRows AS
(
    SELECT
        stage.CustSid,
        stage.RawPhoneNumber,
        stage.IsVip,
        stage.ImportedPhoneSource,
        phone.Id AS PhoneNumberRecordId
    FROM #ImportPhoneRows AS stage
    INNER JOIN dbo.PhoneNumberRecords AS phone
        ON phone.CanonicalPhoneNumber = stage.CanonicalPhoneNumber
)
UPDATE target
SET
    target.IsVip = source.IsVip,
    target.RawPhoneNumber = source.RawPhoneNumber,
    target.LastImportedAtUtc = @UtcNow,
    target.ImportBatchId = @ImportBatchId
FROM dbo.CustomerPhoneLinks AS target
INNER JOIN ResolvedRows AS source
    ON source.CustSid = target.CustSid
    AND source.PhoneNumberRecordId = target.PhoneNumberRecordId
    AND source.ImportedPhoneSource = target.ImportedPhoneSource;

;WITH ResolvedRows AS
(
    SELECT
        stage.CustSid,
        stage.RawPhoneNumber,
        stage.IsVip,
        stage.ImportedPhoneSource,
        phone.Id AS PhoneNumberRecordId
    FROM #ImportPhoneRows AS stage
    INNER JOIN dbo.PhoneNumberRecords AS phone
        ON phone.CanonicalPhoneNumber = stage.CanonicalPhoneNumber
)
INSERT INTO dbo.CustomerPhoneLinks
(
    CustSid,
    IsVip,
    ImportedPhoneSource,
    RawPhoneNumber,
    CreatedAtUtc,
    LastImportedAtUtc,
    PhoneNumberRecordId,
    ImportBatchId
)
SELECT
    source.CustSid,
    source.IsVip,
    source.ImportedPhoneSource,
    source.RawPhoneNumber,
    @UtcNow,
    @UtcNow,
    source.PhoneNumberRecordId,
    @ImportBatchId
FROM ResolvedRows AS source
LEFT JOIN dbo.CustomerPhoneLinks AS target
    ON target.CustSid = source.CustSid
    AND target.PhoneNumberRecordId = source.PhoneNumberRecordId
    AND target.ImportedPhoneSource = source.ImportedPhoneSource
WHERE target.Id IS NULL;

SET @RowsImported = @@ROWCOUNT;

UPDATE dbo.ImportBatches
SET
    CompletedAtUtc = @UtcNow,
    RowsRead = 1,
    RowsImported = @RowsImported,
    RowsRejected = 0,
    Status = 'Completed'
WHERE Id = @ImportBatchId;

COMMIT TRANSACTION;

SELECT
    batch.Id AS ImportBatchId,
    batch.Status,
    batch.RowsRead,
    batch.RowsImported,
    batch.RowsRejected,
    link.CustSid,
    link.IsVip,
    link.ImportedPhoneSource,
    link.RawPhoneNumber,
    record.CanonicalPhoneNumber,
    record.CampaignEligibilityStatus,
    record.LastEnrichedAtUtc
FROM dbo.ImportBatches AS batch
INNER JOIN dbo.CustomerPhoneLinks AS link
    ON link.ImportBatchId = batch.Id
INNER JOIN dbo.PhoneNumberRecords AS record
    ON record.Id = link.PhoneNumberRecordId
WHERE batch.Id = @ImportBatchId;
