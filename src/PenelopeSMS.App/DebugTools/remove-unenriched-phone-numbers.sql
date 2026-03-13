SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @TargetPhoneNumbers TABLE
(
    Id int NOT NULL PRIMARY KEY
);

INSERT INTO @TargetPhoneNumbers (Id)
SELECT pnr.Id
FROM dbo.PhoneNumberRecords AS pnr
WHERE pnr.LastEnrichedAtUtc IS NULL;

DECLARE @TargetRecipients TABLE
(
    Id int NOT NULL PRIMARY KEY
);

INSERT INTO @TargetRecipients (Id)
SELECT cr.Id
FROM dbo.CampaignRecipients AS cr
INNER JOIN @TargetPhoneNumbers AS target
    ON target.Id = cr.PhoneNumberRecordId;

DELETE history
FROM dbo.CampaignRecipientStatusHistory AS history
INNER JOIN @TargetRecipients AS target
    ON target.Id = history.CampaignRecipientId;

DECLARE @DeletedStatusHistory int = @@ROWCOUNT;

DELETE recipient
FROM dbo.CampaignRecipients AS recipient
INNER JOIN @TargetRecipients AS target
    ON target.Id = recipient.Id;

DECLARE @DeletedRecipients int = @@ROWCOUNT;

DELETE link
FROM dbo.CustomerPhoneLinks AS link
INNER JOIN @TargetPhoneNumbers AS target
    ON target.Id = link.PhoneNumberRecordId;

DECLARE @DeletedCustomerLinks int = @@ROWCOUNT;

DELETE phone
FROM dbo.PhoneNumberRecords AS phone
INNER JOIN @TargetPhoneNumbers AS target
    ON target.Id = phone.Id;

DECLARE @DeletedPhoneNumbers int = @@ROWCOUNT;

COMMIT TRANSACTION;

SELECT
    @DeletedPhoneNumbers AS DeletedPhoneNumberRecords,
    @DeletedCustomerLinks AS DeletedCustomerPhoneLinks,
    @DeletedRecipients AS DeletedCampaignRecipients,
    @DeletedStatusHistory AS DeletedCampaignRecipientStatusHistory;
