using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Services;
using PenelopeSMS.Infrastructure.SqlServer;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;

namespace PenelopeSMS.Tests.Data;

public sealed class ImportPersistenceTests
{
    private readonly PhoneNumberNormalizer normalizer = new();

    [Fact]
    public async Task PersistAsyncCreatesOneCanonicalPhoneForMultipleCustomerLinks()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var importService = new ImportPersistenceService(database.DbContext);
        var importBatch = await importService.StartBatchAsync();

        var firstResult = await importService.PersistAsync(
            importBatch.Id,
            "CUST-001",
            normalizer.Normalize("650-253-0000", "US"));

        var secondResult = await importService.PersistAsync(
            importBatch.Id,
            "CUST-002",
            normalizer.Normalize("+1 (650) 253-0000", "US"));

        var phoneNumberRecords = await database.DbContext.PhoneNumberRecords.ToListAsync();
        var customerPhoneLinks = await database.DbContext.CustomerPhoneLinks
            .OrderBy(link => link.CustSid)
            .ToListAsync();

        Assert.Single(phoneNumberRecords);
        Assert.Equal(2, customerPhoneLinks.Count);
        Assert.True(firstResult.CreatedPhoneNumber);
        Assert.False(secondResult.CreatedPhoneNumber);
        Assert.True(firstResult.CreatedCustomerLink);
        Assert.True(secondResult.CreatedCustomerLink);
        Assert.All(customerPhoneLinks, link => Assert.Equal(phoneNumberRecords[0].Id, link.PhoneNumberRecordId));
    }

    [Fact]
    public async Task CompleteBatchAsyncUpdatesAuditFieldsWithoutDuplicatingExistingLinks()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var importService = new ImportPersistenceService(database.DbContext);
        var importBatch = await importService.StartBatchAsync();
        var normalizedPhoneNumber = normalizer.Normalize("650-253-0000", "US");

        await importService.PersistAsync(importBatch.Id, "CUST-001", normalizedPhoneNumber);
        var repeatedResult = await importService.PersistAsync(importBatch.Id, "CUST-001", normalizedPhoneNumber);

        await importService.CompleteBatchAsync(
            importBatch.Id,
            rowsRead: 2,
            rowsImported: 1,
            rowsRejected: 1);

        var storedBatch = await database.DbContext.ImportBatches.SingleAsync();
        var linkCount = await database.DbContext.CustomerPhoneLinks.CountAsync();

        Assert.False(repeatedResult.CreatedCustomerLink);
        Assert.Equal(2, storedBatch.RowsRead);
        Assert.Equal(1, storedBatch.RowsImported);
        Assert.Equal(1, storedBatch.RowsRejected);
        Assert.Equal(ImportBatch.CompletedStatus, storedBatch.Status);
        Assert.NotNull(storedBatch.CompletedAtUtc);
        Assert.Equal(1, linkCount);
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
