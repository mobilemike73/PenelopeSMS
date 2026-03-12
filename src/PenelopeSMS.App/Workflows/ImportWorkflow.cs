using Microsoft.Extensions.Options;
using PenelopeSMS.App.Options;
using PenelopeSMS.Domain.Services;
using PenelopeSMS.Infrastructure.Oracle;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;

namespace PenelopeSMS.App.Workflows;

public sealed class ImportWorkflow(
    IOraclePhoneImportReader oraclePhoneImportReader,
    IPhoneNumberNormalizer phoneNumberNormalizer,
    ImportPersistenceService importPersistenceService,
    IOptions<OracleOptions> oracleOptions) : IImportWorkflow
{
    private readonly TextWriter output = Console.Out;

    public async Task<ImportWorkflowResult> RunAsync(CancellationToken cancellationToken = default)
    {
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
                }
            }

            await importPersistenceService.CompleteBatchAsync(
                importBatch.Id,
                rowsRead,
                rowsImported,
                rowsRejected,
                cancellationToken);

            return new ImportWorkflowResult(importBatch.Id, rowsRead, rowsImported, rowsRejected);
        }
        catch
        {
            await importPersistenceService.FailBatchAsync(
                importBatch.Id,
                rowsRead,
                rowsImported,
                rowsRejected,
                cancellationToken);
            throw;
        }
    }
}
