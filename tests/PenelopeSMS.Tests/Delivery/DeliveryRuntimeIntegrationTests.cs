using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PenelopeSMS.App.Monitoring;
using PenelopeSMS.App.Options;
using PenelopeSMS.App.Services;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.CallbackBridge.Models;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.Aws;
using PenelopeSMS.Infrastructure.SqlServer;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;

namespace PenelopeSMS.Tests.Delivery;

public sealed class DeliveryRuntimeIntegrationTests
{
    [Fact]
    public async Task WorkerProcessesBridgeEnvelopeEndToEndAndEmitsVerboseOutput()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedRecipientAsync(database.DbContext);

        var workflow = new DeliveryCallbackProcessingWorkflow(
            new DeliveryCallbackRepository(database.DbContext),
            new RejectedDeliveryCallbackRepository(database.DbContext));

        var envelope = TwilioCallbackEnvelope.CreateDelivery(
            messageSid: "SM123",
            messageStatus: "delivered",
            providerErrorCode: null,
            providerErrorMessage: null,
            providerEventRawValue: "2603131205",
            rawPayloadJson: """{"Body":"MessageStatus=delivered"}""",
            receivedAtUtc: new DateTime(2026, 03, 13, 12, 06, 00, DateTimeKind.Utc),
            signatureHeader: "sig");

        var output = new StringWriter();
        var monitor = new OperationsMonitor();
        var sqsClient = new FakeAwsSqsClient(
            new SqsQueueMessage(
                MessageId: "message-1",
                Body: JsonSerializer.Serialize(envelope),
                ReceiptHandle: "receipt-1"));

        var worker = new DeliveryCallbackWorker(
            sqsClient,
            Options.Create(new AwsOptions
            {
                CallbackQueueUrl = "https://sqs.example.com/queue"
            }),
            workflow.ProcessAsync,
            monitor,
            output);

        await worker.ProcessSingleIterationAsync();

        var recipient = await database.DbContext.CampaignRecipients
            .Include(candidate => candidate.StatusHistory)
            .SingleAsync();

        Assert.Equal(CampaignRecipientStatus.Delivered, recipient.Status);
        Assert.Equal(new DateTime(2026, 03, 13, 12, 05, 00, DateTimeKind.Utc), recipient.CurrentStatusAtUtc);
        Assert.Equal(DeliveryEventTimeSource.RawDlrDoneDate, recipient.CurrentStatusTimeSource);
        Assert.Single(recipient.StatusHistory);
        Assert.Equal(1, sqsClient.DeleteCalls);
        Assert.Contains("Processed callback queue message message-1: applied", output.ToString());
        Assert.Contains("Deleted queue message message-1 after applied.", output.ToString());
    }

    private static async Task SeedRecipientAsync(PenelopeSmsDbContext dbContext)
    {
        var campaign = new Campaign
        {
            Name = "Phase 4",
            TemplateFilePath = @"C:\templates\phase4.txt",
            TemplateBody = "Hi",
            BatchSize = 10
        };

        var phoneNumberRecord = new PhoneNumberRecord
        {
            CanonicalPhoneNumber = "+16502530000"
        };

        dbContext.CampaignRecipients.Add(new CampaignRecipient
        {
            Campaign = campaign,
            PhoneNumberRecord = phoneNumberRecord,
            Status = CampaignRecipientStatus.Submitted,
            TwilioMessageSid = "SM123"
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeAwsSqsClient(SqsQueueMessage? nextMessage) : IAwsSqsClient
    {
        public int DeleteCalls { get; private set; }

        public Task<SqsQueueMessage?> ReceiveMessageAsync(string queueUrl, CancellationToken cancellationToken = default)
        {
            var message = nextMessage;
            nextMessage = null;
            return Task.FromResult(message);
        }

        public Task DeleteMessageAsync(string queueUrl, string receiptHandle, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            return Task.CompletedTask;
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
