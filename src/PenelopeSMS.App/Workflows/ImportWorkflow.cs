using Microsoft.Extensions.Options;
using PenelopeSMS.App.Monitoring;
using PenelopeSMS.App.Options;
using PenelopeSMS.Domain.Services;
using PenelopeSMS.Infrastructure.Oracle;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;

namespace PenelopeSMS.App.Workflows;

public sealed class ImportWorkflow(
    IOraclePhoneImportReader oraclePhoneImportReader,
    IPhoneNumberNormalizer phoneNumberNormalizer,
    ImportPersistenceService importPersistenceService,
    IOptions<OracleOptions> oracleOptions,
    IOperationsMonitor? runtimeOperationsMonitor = null) : IImportWorkflow
{
    private readonly IOperationsMonitor operationsMonitor = runtimeOperationsMonitor ?? NullOperationsMonitor.Instance;
    private readonly TextWriter output = Console.Out;

    public async Task<ImportWorkflowResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var jobId = operationsMonitor.StartJob(OperationType.Import, "Oracle import", "Starting import");
        var importBatch = await importPersistenceService.StartBatchAsync(cancellationToken);
        var rowsRead = 0;
        var rowsImported = 0;
        var rowsRejected = 0;

        try
        {
            await foreach (var row in oraclePhoneImportReader.ReadRowsAsync(cancellationToken))
            {
                rowsRead++;

                try
                {
                    var normalizedPhoneNumber = phoneNumberNormalizer.Normalize(
                        row.PhoneNumber,
                        oracleOptions.Value.DefaultRegion);

                    var persistenceResult = await importPersistenceService.PersistAsync(
                        importBatch.Id,
                        row.CustSid,
                        normalizedPhoneNumber,
                        cancellationToken);

                    if (persistenceResult.CreatedCustomerLink)
                    {
                        rowsImported++;
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    rowsRejected++;
                    output.WriteLine($"Rejected row for CUST_SID {row.CustSid}: {ex.Message}");
                    operationsMonitor.Warn(
                        OperationType.Import,
                        $"Rejected row for CUST_SID {row.CustSid}: {ex.Message}",
                        jobId);
                }

                operationsMonitor.UpdateJob(
                    jobId,
                    $"Read {rowsRead}, Imported {rowsImported}, Rejected {rowsRejected}");
            }

            await importPersistenceService.CompleteBatchAsync(
                importBatch.Id,
                rowsRead,
                rowsImported,
                rowsRejected,
                cancellationToken);

            operationsMonitor.CompleteJob(
                jobId,
                $"Import batch {importBatch.Id} complete. Read: {rowsRead}, Imported: {rowsImported}, Rejected: {rowsRejected}");

            return new ImportWorkflowResult(importBatch.Id, rowsRead, rowsImported, rowsRejected);
        }
        catch (Exception exception)
        {
            await importPersistenceService.FailBatchAsync(
                importBatch.Id,
                rowsRead,
                rowsImported,
                rowsRejected,
                cancellationToken);
            operationsMonitor.Warn(OperationType.Import, exception.Message, jobId);
            operationsMonitor.FailJob(
                jobId,
                $"Import batch {importBatch.Id} failed after Read: {rowsRead}, Imported: {rowsImported}, Rejected: {rowsRejected}");
            throw;
        }
    }
}
