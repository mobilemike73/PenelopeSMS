using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PenelopeSMS.App.Options;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Services;
using PenelopeSMS.Infrastructure.Oracle;
using PenelopeSMS.Infrastructure.SqlServer;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;

namespace PenelopeSMS.Tests.Import;

public sealed class ImportWorkflowTests
{
    private readonly PhoneNumberNormalizer normalizer = new();
    private readonly IOptions<OracleOptions> oracleOptions = Options.Create(new OracleOptions
    {
        DefaultRegion = "US"
    });

    [Fact]
    public async Task RunAsyncPersistsCanonicalRowsForMultipleCustomerLinks()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var workflow = CreateWorkflow(
            database.DbContext,
            new FakeOraclePhoneImportReader(
            [
                new OracleCustomerPhoneRow("CUST-001", "650-253-0000"),
                new OracleCustomerPhoneRow("CUST-002", "+1 (650) 253-0000")
            ]));

        var result = await workflow.RunAsync();
        var batch = await database.DbContext.ImportBatches.SingleAsync();
        var phoneCount = await database.DbContext.PhoneNumberRecords.CountAsync();
        var linkCount = await database.DbContext.CustomerPhoneLinks.CountAsync();

        Assert.Equal(2, result.RowsRead);
        Assert.Equal(2, result.RowsImported);
        Assert.Equal(0, result.RowsRejected);
        Assert.Equal(ImportBatch.CompletedStatus, batch.Status);
        Assert.Equal(1, phoneCount);
        Assert.Equal(2, linkCount);
    }

    [Fact]
    public async Task RunAsyncCountsRejectedRowsWithoutStoppingTheBatch()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var workflow = CreateWorkflow(
            database.DbContext,
            new FakeOraclePhoneImportReader(
            [
                new OracleCustomerPhoneRow("CUST-001", "650-253-0000"),
                new OracleCustomerPhoneRow("CUST-003", "123")
            ]));

        var result = await workflow.RunAsync();
        var batch = await database.DbContext.ImportBatches.SingleAsync();

        Assert.Equal(2, result.RowsRead);
        Assert.Equal(1, result.RowsImported);
        Assert.Equal(1, result.RowsRejected);
        Assert.Equal(ImportBatch.CompletedStatus, batch.Status);
        Assert.Equal(1, await database.DbContext.CustomerPhoneLinks.CountAsync());
    }

    [Fact]
    public async Task RunAsyncMarksBatchFailedWhenReaderThrows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var workflow = CreateWorkflow(
            database.DbContext,
            new FakeOraclePhoneImportReader(
                [new OracleCustomerPhoneRow("CUST-001", "650-253-0000")],
                new InvalidOperationException("Oracle connection dropped.")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.RunAsync());

        var batch = await database.DbContext.ImportBatches.SingleAsync();

        Assert.Equal(ImportBatch.FailedStatus, batch.Status);
        Assert.Equal(1, batch.RowsRead);
        Assert.Equal(1, batch.RowsImported);
        Assert.Equal(0, batch.RowsRejected);
        Assert.NotNull(batch.CompletedAtUtc);
    }

    private ImportWorkflow CreateWorkflow(
        PenelopeSmsDbContext dbContext,
        IOraclePhoneImportReader oraclePhoneImportReader)
    {
        return new ImportWorkflow(
            oraclePhoneImportReader,
            normalizer,
            new ImportPersistenceService(dbContext),
            oracleOptions);
    }

    private sealed class FakeOraclePhoneImportReader(
        IReadOnlyList<OracleCustomerPhoneRow> rows,
        Exception? exceptionAfterRows = null) : IOraclePhoneImportReader
    {
        public async IAsyncEnumerable<OracleCustomerPhoneRow> ReadRowsAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return row;
                await Task.Yield();
            }

            if (exceptionAfterRows is not null)
            {
                throw exceptionAfterRows;
            }
        }
    }

    private sealed class SqliteTestDatabase : IAsyncDisposable
    {
        private SqliteTestDatabase(SqliteConnection connection, PenelopeSmsDbContext dbContext)
        {
            Connection = connection;
            DbContext = dbContext;
        }

        public SqliteConnection Connection { get; }

        public PenelopeSmsDbContext DbContext { get; }

        public static async Task<SqliteTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<PenelopeSmsDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new PenelopeSmsDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            return new SqliteTestDatabase(connection, dbContext);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
