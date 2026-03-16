using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PenelopeSMS.App.Monitoring;
using PenelopeSMS.App.Options;
using PenelopeSMS.App.Services;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Domain.Services;
using PenelopeSMS.Infrastructure.Aws;
using PenelopeSMS.Infrastructure.Oracle;
using PenelopeSMS.Infrastructure.SqlServer;
using PenelopeSMS.Infrastructure.SqlServer.Queries;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;
using PenelopeSMS.Infrastructure.Twilio;

namespace PenelopeSMS.Tests.Monitoring;

public sealed class WorkflowMonitoringInstrumentationTests
{
    [Fact]
    public async Task ImportWorkflowPublishesCompletedJobSummary()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var monitor = new OperationsMonitor();
        var workflow = new ImportWorkflow(
            new FakeOraclePhoneImportReader([new OracleCustomerPhoneRow("CUST-001", "650-253-0000", false, ImportedPhoneSource.Phone1)]),
            new PhoneNumberNormalizer(),
            new ImportPersistenceService(database.DbContext),
            Options.Create(new OracleOptions { DefaultRegion = "US" }),
            monitor);

        await workflow.RunAsync();

        var snapshot = monitor.GetSnapshot();
        Assert.Contains(snapshot.CompletedJobs, job => job.OperationType == OperationType.Import);
        Assert.Empty(snapshot.ActiveJobs);
    }

    [Fact]
    public async Task EnrichmentWorkflowPublishesWarningsAndResolvesThemOnCompletion()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var monitor = new OperationsMonitor();
        var record = new PhoneNumberRecord
        {
            CanonicalPhoneNumber = "+16502530000"
        };

        database.DbContext.PhoneNumberRecords.Add(record);
        await database.DbContext.SaveChangesAsync();

        var workflow = new EnrichmentWorkflow(
            new EnrichmentTargetingQuery(database.DbContext),
            new PhoneNumberEnrichmentRepository(database.DbContext),
            new FakeTwilioLookupClient(new Dictionary<string, TwilioLookupResult>
            {
                [record.CanonicalPhoneNumber] = TwilioLookupResult.Failure(
                    EnrichmentFailureStatus.Retryable,
                    "lookup_timeout",
                    "Provider timeout")
            }),
            monitor);

        await workflow.RunAsync(fullRefresh: true);

        var snapshot = monitor.GetSnapshot();
        Assert.Contains(snapshot.CompletedJobs, job => job.OperationType == OperationType.Enrichment);
        Assert.Empty(snapshot.ActiveWarnings);
    }

    [Fact]
    public async Task CampaignSendWorkflowPublishesCompletedJobSummary()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var monitor = new OperationsMonitor();
        var campaign = new Campaign
        {
            Name = "Spring",
            TemplateFilePath = @"C:\templates\spring.txt",
            TemplateBody = "Hello",
            BatchSize = 1
        };
        var phoneRecord = new PhoneNumberRecord
        {
            CanonicalPhoneNumber = "+16502530001",
            CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible
        };

        database.DbContext.Campaigns.Add(campaign);
        database.DbContext.PhoneNumberRecords.Add(phoneRecord);
        await database.DbContext.SaveChangesAsync();

        database.DbContext.CampaignRecipients.Add(new CampaignRecipient
        {
            CampaignId = campaign.Id,
            PhoneNumberRecordId = phoneRecord.Id,
            Status = CampaignRecipientStatus.Pending
        });
        await database.DbContext.SaveChangesAsync();

        var workflow = new CampaignSendWorkflow(
            new CampaignSendBatchQuery(database.DbContext),
            new CampaignSendRepository(database.DbContext),
            new FakeTwilioMessageSender(),
            monitor);

        await workflow.SendNextBatchAsync(campaign.Id);

        var snapshot = monitor.GetSnapshot();
        Assert.Contains(snapshot.CompletedJobs, job => job.OperationType == OperationType.CampaignSend);
    }

    [Fact]
    public async Task DeliveryCallbackWorkerPublishesLiveDeliveryLines()
    {
        var monitor = new OperationsMonitor();
        var worker = new DeliveryCallbackWorker(
            new FakeAwsSqsClient(new SqsQueueMessage("message-1", "{}", "receipt-1")),
            Options.Create(new AwsOptions
            {
                CallbackQueueUrl = "https://sqs.example.com/queue"
            }),
            (_, _) => Task.FromResult(new DeliveryCallbackProcessingResult(true, "applied", "applied")),
            monitor,
            TextWriter.Null);

        await worker.ProcessSingleIterationAsync();

        var snapshot = monitor.GetSnapshot();
        Assert.Contains(snapshot.ActiveJobs, job => job.OperationType == OperationType.DeliveryProcessing);
        Assert.Contains(snapshot.LiveDeliveryLines, line => line.Contains("applied", StringComparison.Ordinal));
    }

    private sealed class FakeOraclePhoneImportReader(IReadOnlyList<OracleCustomerPhoneRow> rows) : IOraclePhoneImportReader
    {
        public async IAsyncEnumerable<OracleCustomerPhoneRow> ReadRowsAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var row in rows)
            {
                yield return row;
                await Task.Yield();
            }
        }
    }

    private sealed class FakeTwilioLookupClient(Dictionary<string, TwilioLookupResult> results) : ITwilioLookupClient
    {
        public Task<TwilioLookupResult> LookupAsync(string phoneNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(results[phoneNumber]);
        }
    }

    private sealed class FakeTwilioMessageSender : ITwilioMessageSender
    {
        public Task<TwilioSendResult> SendAsync(string toPhoneNumber, string messageBody, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TwilioSendResult.Success("SM-1", "queued"));
        }
    }

    private sealed class FakeAwsSqsClient(SqsQueueMessage? nextMessage) : IAwsSqsClient
    {
        public Task<SqsQueueMessage?> ReceiveMessageAsync(string queueUrl, CancellationToken cancellationToken = default)
        {
            var message = nextMessage;
            nextMessage = null;
            return Task.FromResult(message);
        }

        public Task<SqsQueueDepthSnapshot> GetQueueDepthAsync(string queueUrl, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SqsQueueDepthSnapshot(0, 0));
        }

        public Task DeleteMessageAsync(string queueUrl, string receiptHandle, CancellationToken cancellationToken = default)
        {
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
