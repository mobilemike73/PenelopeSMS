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

    [Fact]
    public async Task ProcessAsyncIncludesFailureReasonInConsoleMessage()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var recipient = await CreateRecipientAsync(database.DbContext, "SM123");
        var workflow = new DeliveryCallbackProcessingWorkflow(
            new DeliveryCallbackRepository(database.DbContext),
            new RejectedDeliveryCallbackRepository(database.DbContext));

        var result = await workflow.ProcessAsync(new SqsQueueMessage(
            MessageId: "msg-4",
            Body: """
                {"EnvelopeType":"delivery","MessageSid":"SM123","MessageStatus":"failed","ProviderErrorCode":"21610","ProviderErrorMessage":"Attempt to send to unsubscribed recipient","RawPayloadJson":"{}","ReceivedAtUtc":"2026-03-13T00:00:00Z"}
                """,
            ReceiptHandle: "receipt-4"));

        Assert.True(result.ShouldDeleteMessage);
        Assert.Equal("failed", result.MessageStatus);
        Assert.Equal("21610", result.FailureCode);
        Assert.Equal("Attempt to send to unsubscribed recipient", result.FailureMessage);
        Assert.Contains("Reason: 21610 | Attempt to send to unsubscribed recipient", result.ConsoleMessage);
    }

    private static async Task<PenelopeSMS.Domain.Entities.CampaignRecipient> CreateRecipientAsync(
        PenelopeSmsDbContext dbContext,
        string messageSid)
    {
        var record = new PenelopeSMS.Domain.Entities.PhoneNumberRecord
        {
            CanonicalPhoneNumber = "+15555550123",
            CreatedAtUtc = new DateTime(2026, 03, 13, 0, 0, 0, DateTimeKind.Utc),
            LastImportedAtUtc = new DateTime(2026, 03, 13, 0, 0, 0, DateTimeKind.Utc)
        };

        var campaign = new PenelopeSMS.Domain.Entities.Campaign
        {
            Name = "Test",
            TemplateFilePath = "test.txt",
            TemplateBody = "Hello",
            BatchSize = 1,
            Status = PenelopeSMS.Domain.Enums.CampaignStatus.Draft
        };

        var recipient = new PenelopeSMS.Domain.Entities.CampaignRecipient
        {
            Campaign = campaign,
            PhoneNumberRecord = record,
            Status = PenelopeSMS.Domain.Enums.CampaignRecipientStatus.Sent,
            TwilioMessageSid = messageSid,
            SubmittedAtUtc = new DateTime(2026, 03, 13, 0, 0, 0, DateTimeKind.Utc),
            LastAttemptedAtUtc = new DateTime(2026, 03, 13, 0, 0, 0, DateTimeKind.Utc),
            CurrentStatusAtUtc = new DateTime(2026, 03, 13, 0, 0, 0, DateTimeKind.Utc),
            CurrentStatusTimeSource = PenelopeSMS.Domain.Enums.DeliveryEventTimeSource.CallbackReceivedAt
        };

        dbContext.CampaignRecipients.Add(recipient);
        await dbContext.SaveChangesAsync();
        return recipient;
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
