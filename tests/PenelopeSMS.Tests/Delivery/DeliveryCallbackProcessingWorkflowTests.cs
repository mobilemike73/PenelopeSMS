using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Infrastructure.Aws;
using PenelopeSMS.Infrastructure.SqlServer;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;

namespace PenelopeSMS.Tests.Delivery;

public sealed class DeliveryCallbackProcessingWorkflowTests
{
    [Fact]
    public async Task ProcessAsyncStoresRejectedCallbacksFromBridgeEnvelopes()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var workflow = new DeliveryCallbackProcessingWorkflow(
            new DeliveryCallbackRepository(database.DbContext),
            new RejectedDeliveryCallbackRepository(database.DbContext));

        var result = await workflow.ProcessAsync(new SqsQueueMessage(
            MessageId: "msg-1",
            Body: """
                {"EnvelopeType":"rejected","RejectionReason":"invalid_signature","RawPayloadJson":"{}","ReceivedAtUtc":"2026-03-13T00:00:00Z","SignatureHeader":"sig","ErrorMessage":"bad"}
                """,
            ReceiptHandle: "receipt-1"));

        var rejectedCallbacks = await database.DbContext.RejectedDeliveryCallbacks.ToListAsync();

        Assert.True(result.ShouldDeleteMessage);
        Assert.Equal("rejected_callback", result.Outcome);
        Assert.Single(rejectedCallbacks);
        Assert.Equal("invalid_signature", rejectedCallbacks[0].RejectionReason);
    }

    [Fact]
    public async Task ProcessAsyncRejectsMalformedQueueMessagesWithoutThrowing()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var workflow = new DeliveryCallbackProcessingWorkflow(
            new DeliveryCallbackRepository(database.DbContext),
            new RejectedDeliveryCallbackRepository(database.DbContext));

        var result = await workflow.ProcessAsync(new SqsQueueMessage(
            MessageId: "msg-2",
            Body: "{not-json}",
            ReceiptHandle: "receipt-2"));

        Assert.True(result.ShouldDeleteMessage);
        Assert.Equal("rejected_malformed_queue", result.Outcome);
        Assert.Single(await database.DbContext.RejectedDeliveryCallbacks.ToListAsync());
    }

    [Fact]
    public async Task ProcessAsyncRejectsUnsupportedDeliveryStatuses()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var workflow = new DeliveryCallbackProcessingWorkflow(
            new DeliveryCallbackRepository(database.DbContext),
            new RejectedDeliveryCallbackRepository(database.DbContext));

        var result = await workflow.ProcessAsync(new SqsQueueMessage(
            MessageId: "msg-3",
            Body: """
                {"EnvelopeType":"delivery","MessageSid":"SM123","MessageStatus":"accepted","RawPayloadJson":"{}","ReceivedAtUtc":"2026-03-13T00:00:00Z"}
                """,
            ReceiptHandle: "receipt-3"));

        var rejectedCallback = await database.DbContext.RejectedDeliveryCallbacks.SingleAsync();

        Assert.True(result.ShouldDeleteMessage);
        Assert.Equal("rejected_unsupported_status", result.Outcome);
        Assert.Equal("unsupported_delivery_status", rejectedCallback.RejectionReason);
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
