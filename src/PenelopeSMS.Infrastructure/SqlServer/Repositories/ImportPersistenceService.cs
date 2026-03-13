using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Domain.Services;

namespace PenelopeSMS.Infrastructure.SqlServer.Repositories;

public sealed class ImportPersistenceService(PenelopeSmsDbContext dbContext)
{
    private const string ImportStageTableName = "#ImportPhoneRows";
    private const string CreateImportStageTableSql = """
        CREATE TABLE #ImportPhoneRows
        (
            CustSid nvarchar(64) NOT NULL,
            RawPhoneNumber nvarchar(64) NOT NULL,
            CanonicalPhoneNumber nvarchar(32) NOT NULL,
            IsVip bit NOT NULL,
            ImportedPhoneSource int NOT NULL
        );
        """;

    private const string UpsertImportRowsSql = """
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
            CampaignEligibilityStatus,
            EnrichmentFailureStatus
        )
        SELECT
            source.CanonicalPhoneNumber,
            @utcNow,
            @utcNow,
            0,
            0
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
            target.LastImportedAtUtc = @utcNow
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
            target.LastImportedAtUtc = @utcNow,
            target.ImportBatchId = @importBatchId
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
            @utcNow,
            @utcNow,
            source.PhoneNumberRecordId,
            @importBatchId
        FROM ResolvedRows AS source
        LEFT JOIN dbo.CustomerPhoneLinks AS target
            ON target.CustSid = source.CustSid
            AND target.PhoneNumberRecordId = source.PhoneNumberRecordId
            AND target.ImportedPhoneSource = source.ImportedPhoneSource
        WHERE target.Id IS NULL;

        SELECT CAST(@@ROWCOUNT AS int);
        """;

    public async Task<ImportBatch> StartBatchAsync(CancellationToken cancellationToken = default)
    {
        var importBatch = new ImportBatch
        {
            StartedAtUtc = DateTime.UtcNow,
            Status = ImportBatch.InProgressStatus
        };

        dbContext.ImportBatches.Add(importBatch);
        await dbContext.SaveChangesAsync(cancellationToken);

        return importBatch;
    }

    public async Task ResetImportDataAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.CampaignRecipientStatusHistory.ExecuteDeleteAsync(cancellationToken);
        await dbContext.CampaignRecipients.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Campaigns.ExecuteDeleteAsync(cancellationToken);
        await dbContext.CustomerPhoneLinks.ExecuteDeleteAsync(cancellationToken);
        await dbContext.PhoneNumberRecords.ExecuteDeleteAsync(cancellationToken);
        await dbContext.ImportBatches.ExecuteDeleteAsync(cancellationToken);

        if (dbContext.Database.IsSqlServer())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                DBCC CHECKIDENT ('CampaignRecipientStatusHistory', RESEED, 0);
                DBCC CHECKIDENT ('CampaignRecipients', RESEED, 0);
                DBCC CHECKIDENT ('Campaigns', RESEED, 0);
                DBCC CHECKIDENT ('CustomerPhoneLinks', RESEED, 0);
                DBCC CHECKIDENT ('PhoneNumberRecords', RESEED, 0);
                DBCC CHECKIDENT ('ImportBatches', RESEED, 0);
                """,
                cancellationToken);
        }
    }

    public async Task<int> PersistManyAsync(
        int importBatchId,
        IReadOnlyCollection<PersistPhoneNumberRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(importBatchId, 1);
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0)
        {
            return 0;
        }

        var importBatchExists = await dbContext.ImportBatches
            .AnyAsync(batch => batch.Id == importBatchId, cancellationToken);

        if (!importBatchExists)
        {
            throw new InvalidOperationException($"Import batch {importBatchId} was not found.");
        }

        var deduplicatedRequests = requests
            .GroupBy(request => new
            {
                request.CustSid,
                request.CanonicalPhoneNumber,
                request.ImportedPhoneSource
            })
            .Select(group => group.Last())
            .ToArray();

        if (!dbContext.Database.IsSqlServer())
        {
            return await PersistManyFallbackAsync(importBatchId, deduplicatedRequests, cancellationToken);
        }

        return await PersistManySqlServerAsync(importBatchId, deduplicatedRequests, cancellationToken);
    }

    public async Task<PersistPhoneNumberResult> PersistAsync(
        int importBatchId,
        string custSid,
        bool isVip,
        ImportedPhoneSource importedPhoneSource,
        PhoneNumberNormalizationResult normalizedPhoneNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(custSid);

        var importBatch = await dbContext.ImportBatches
            .SingleOrDefaultAsync(batch => batch.Id == importBatchId, cancellationToken)
            ?? throw new InvalidOperationException($"Import batch {importBatchId} was not found.");

        var utcNow = DateTime.UtcNow;

        var phoneNumberRecord = await dbContext.PhoneNumberRecords
            .SingleOrDefaultAsync(
                record => record.CanonicalPhoneNumber == normalizedPhoneNumber.CanonicalPhoneNumber,
                cancellationToken);

        var createdPhoneNumber = false;

        if (phoneNumberRecord is null)
        {
            phoneNumberRecord = new PhoneNumberRecord
            {
                CanonicalPhoneNumber = normalizedPhoneNumber.CanonicalPhoneNumber,
                CreatedAtUtc = utcNow,
                LastImportedAtUtc = utcNow
            };

            dbContext.PhoneNumberRecords.Add(phoneNumberRecord);
            await dbContext.SaveChangesAsync(cancellationToken);
            createdPhoneNumber = true;
        }
        else
        {
            phoneNumberRecord.LastImportedAtUtc = utcNow;
        }

        var customerPhoneLink = await dbContext.CustomerPhoneLinks
            .SingleOrDefaultAsync(
                link => link.CustSid == custSid
                    && link.PhoneNumberRecordId == phoneNumberRecord.Id
                    && link.ImportedPhoneSource == importedPhoneSource,
                cancellationToken);

        var createdCustomerLink = false;

        if (customerPhoneLink is null)
        {
            customerPhoneLink = new CustomerPhoneLink
            {
                CustSid = custSid,
                IsVip = isVip,
                ImportedPhoneSource = importedPhoneSource,
                RawPhoneNumber = normalizedPhoneNumber.RawInput,
                CreatedAtUtc = utcNow,
                LastImportedAtUtc = utcNow,
                ImportBatchId = importBatch.Id,
                PhoneNumberRecordId = phoneNumberRecord.Id
            };

            dbContext.CustomerPhoneLinks.Add(customerPhoneLink);
            createdCustomerLink = true;
        }
        else
        {
            customerPhoneLink.IsVip = isVip;
            customerPhoneLink.ImportedPhoneSource = importedPhoneSource;
            customerPhoneLink.RawPhoneNumber = normalizedPhoneNumber.RawInput;
            customerPhoneLink.LastImportedAtUtc = utcNow;
            customerPhoneLink.ImportBatchId = importBatch.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PersistPhoneNumberResult(
            phoneNumberRecord.Id,
            createdPhoneNumber,
            createdCustomerLink);
    }

    private async Task<int> PersistManyFallbackAsync(
        int importBatchId,
        IReadOnlyCollection<PersistPhoneNumberRequest> requests,
        CancellationToken cancellationToken)
    {
        var createdCustomerLinks = 0;

        foreach (var request in requests)
        {
            var result = await PersistAsync(
                importBatchId,
                request.CustSid,
                request.IsVip,
                request.ImportedPhoneSource,
                new PhoneNumberNormalizationResult(
                    request.RawPhoneNumber,
                    request.CanonicalPhoneNumber,
                    string.Empty),
                cancellationToken);

            if (result.CreatedCustomerLink)
            {
                createdCustomerLinks++;
            }
        }

        return createdCustomerLinks;
    }

    private async Task<int> PersistManySqlServerAsync(
        int importBatchId,
        PersistPhoneNumberRequest[] requests,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var connection = (SqlConnection)dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using (var createStageCommand = connection.CreateCommand())
            {
                createStageCommand.Transaction = transaction;
                createStageCommand.CommandText = CreateImportStageTableSql;
                await createStageCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
            {
                bulkCopy.DestinationTableName = ImportStageTableName;
                bulkCopy.BatchSize = requests.Length;
                bulkCopy.EnableStreaming = true;

                bulkCopy.ColumnMappings.Add(nameof(PersistPhoneNumberRequest.CustSid), "CustSid");
                bulkCopy.ColumnMappings.Add(nameof(PersistPhoneNumberRequest.RawPhoneNumber), "RawPhoneNumber");
                bulkCopy.ColumnMappings.Add(nameof(PersistPhoneNumberRequest.CanonicalPhoneNumber), "CanonicalPhoneNumber");
                bulkCopy.ColumnMappings.Add(nameof(PersistPhoneNumberRequest.IsVip), "IsVip");
                bulkCopy.ColumnMappings.Add(nameof(PersistPhoneNumberRequest.ImportedPhoneSource), "ImportedPhoneSource");

                await bulkCopy.WriteToServerAsync(CreateImportDataTable(requests), cancellationToken);
            }

            await using var upsertCommand = connection.CreateCommand();
            upsertCommand.Transaction = transaction;
            upsertCommand.CommandText = UpsertImportRowsSql;
            upsertCommand.Parameters.Add(new SqlParameter("@utcNow", SqlDbType.DateTime2) { Value = utcNow });
            upsertCommand.Parameters.Add(new SqlParameter("@importBatchId", SqlDbType.Int) { Value = importBatchId });

            var result = await upsertCommand.ExecuteScalarAsync(cancellationToken);
            var createdCustomerLinks = result is null or DBNull
                ? 0
                : Convert.ToInt32(result, CultureInfo.InvariantCulture);

            await transaction.CommitAsync(cancellationToken);
            return createdCustomerLinks;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static DataTable CreateImportDataTable(PersistPhoneNumberRequest[] requests)
    {
        var table = new DataTable();
        table.Columns.Add(nameof(PersistPhoneNumberRequest.CustSid), typeof(string));
        table.Columns.Add(nameof(PersistPhoneNumberRequest.RawPhoneNumber), typeof(string));
        table.Columns.Add(nameof(PersistPhoneNumberRequest.CanonicalPhoneNumber), typeof(string));
        table.Columns.Add(nameof(PersistPhoneNumberRequest.IsVip), typeof(bool));
        table.Columns.Add(nameof(PersistPhoneNumberRequest.ImportedPhoneSource), typeof(int));

        foreach (var request in requests)
        {
            table.Rows.Add(
                request.CustSid,
                request.RawPhoneNumber,
                request.CanonicalPhoneNumber,
                request.IsVip,
                (int)request.ImportedPhoneSource);
        }

        return table;
    }

    public async Task CompleteBatchAsync(
        int importBatchId,
        int rowsRead,
        int rowsImported,
        int rowsRejected,
        CancellationToken cancellationToken = default)
    {
        var importBatch = await dbContext.ImportBatches
            .SingleOrDefaultAsync(batch => batch.Id == importBatchId, cancellationToken)
            ?? throw new InvalidOperationException($"Import batch {importBatchId} was not found.");

        importBatch.RowsRead = rowsRead;
        importBatch.RowsImported = rowsImported;
        importBatch.RowsRejected = rowsRejected;
        importBatch.CompletedAtUtc = DateTime.UtcNow;
        importBatch.Status = ImportBatch.CompletedStatus;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task FailBatchAsync(
        int importBatchId,
        int rowsRead,
        int rowsImported,
        int rowsRejected,
        CancellationToken cancellationToken = default)
    {
        var importBatch = await dbContext.ImportBatches
            .SingleOrDefaultAsync(batch => batch.Id == importBatchId, cancellationToken)
            ?? throw new InvalidOperationException($"Import batch {importBatchId} was not found.");

        importBatch.RowsRead = rowsRead;
        importBatch.RowsImported = rowsImported;
        importBatch.RowsRejected = rowsRejected;
        importBatch.CompletedAtUtc = DateTime.UtcNow;
        importBatch.Status = ImportBatch.FailedStatus;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed record PersistPhoneNumberResult(
    int PhoneNumberRecordId,
    bool CreatedPhoneNumber,
    bool CreatedCustomerLink);

public sealed record PersistPhoneNumberRequest(
    string CustSid,
    bool IsVip,
    ImportedPhoneSource ImportedPhoneSource,
    string RawPhoneNumber,
    string CanonicalPhoneNumber);
