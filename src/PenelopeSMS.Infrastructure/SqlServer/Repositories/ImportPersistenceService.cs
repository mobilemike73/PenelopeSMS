using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Services;

namespace PenelopeSMS.Infrastructure.SqlServer.Repositories;

public sealed class ImportPersistenceService(PenelopeSmsDbContext dbContext)
{
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

    public async Task<PersistPhoneNumberResult> PersistAsync(
        int importBatchId,
        string custSid,
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
                link => link.CustSid == custSid && link.PhoneNumberRecordId == phoneNumberRecord.Id,
                cancellationToken);

        var createdCustomerLink = false;

        if (customerPhoneLink is null)
        {
            customerPhoneLink = new CustomerPhoneLink
            {
                CustSid = custSid,
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
            customerPhoneLink.LastImportedAtUtc = utcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PersistPhoneNumberResult(
            phoneNumberRecord.Id,
            createdPhoneNumber,
            createdCustomerLink);
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
