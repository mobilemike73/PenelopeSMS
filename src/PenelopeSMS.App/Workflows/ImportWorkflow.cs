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
    private const int ImportChunkSize = 5_000;
    private const int ProgressUpdateInterval = 500;
    private readonly IOperationsMonitor operationsMonitor = runtimeOperationsMonitor ?? NullOperationsMonitor.Instance;
    private readonly TextWriter output = Console.Out;

    public async Task<ImportWorkflowResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var jobId = operationsMonitor.StartJob(OperationType.Import, "Oracle import", "Starting import");
        var importBatch = await importPersistenceService.StartBatchAsync(cancellationToken);
        var rowsRead = 0;
        var rowsImported = 0;
        var rowsRejected = 0;
        var bufferedRows = new List<PersistPhoneNumberRequest>(ImportChunkSize);

        try
        {
            await foreach (var row in oraclePhoneImportReader.ReadRowsAsync(cancellationToken))
            {
                rowsRead++;

                try
                {
                    if (!phoneNumberNormalizer.TryNormalize(
                        row.PhoneNumber,
                        oracleOptions.Value.DefaultRegion,
                        out var normalizedPhoneNumber,
                        out var normalizationErrorMessage))
                    {
                        rowsRejected++;
                        output.WriteLine($"Rejected row for CUST_SID {row.CustSid}: {normalizationErrorMessage}");
                        operationsMonitor.Warn(
                            OperationType.Import,
                            $"Rejected row for CUST_SID {row.CustSid}: {normalizationErrorMessage}",
                            jobId);
                        continue;
                    }

                    bufferedRows.Add(new PersistPhoneNumberRequest(
                        row.CustSid,
                        row.IsVip,
                        row.ImportedPhoneSource,
                        normalizedPhoneNumber!.RawInput,
                        normalizedPhoneNumber.CanonicalPhoneNumber));

                    if (bufferedRows.Count >= ImportChunkSize)
                    {
                        rowsImported += await FlushBufferedRowsAsync(
                            importBatch.Id,
                            bufferedRows,
                            cancellationToken);
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

                if (rowsRead % ProgressUpdateInterval == 0)
                {
                    operationsMonitor.UpdateJob(
                        jobId,
                        $"Read {rowsRead}, Imported {rowsImported}, Rejected {rowsRejected}");
                }
            }

            rowsImported += await FlushBufferedRowsAsync(
                importBatch.Id,
                bufferedRows,
                cancellationToken);

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
            rowsImported += await FlushBufferedRowsAsync(
                importBatch.Id,
                bufferedRows,
                cancellationToken);

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

    private async Task<int> FlushBufferedRowsAsync(
        int importBatchId,
        List<PersistPhoneNumberRequest> bufferedRows,
        CancellationToken cancellationToken)
    {
        if (bufferedRows.Count == 0)
        {
            return 0;
        }

        var importedCount = await importPersistenceService.PersistManyAsync(
            importBatchId,
            bufferedRows,
            cancellationToken);

        bufferedRows.Clear();
        return importedCount;
    }
}
