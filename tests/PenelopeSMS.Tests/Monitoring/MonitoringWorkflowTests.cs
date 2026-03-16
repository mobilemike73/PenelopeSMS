using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenelopeSMS.App.Monitoring;
using PenelopeSMS.App.Rendering;
using PenelopeSMS.App.Options;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.Aws;
using PenelopeSMS.Infrastructure.SqlServer;
using PenelopeSMS.Infrastructure.SqlServer.Queries;
using Microsoft.Extensions.Options;

namespace PenelopeSMS.Tests.Monitoring;

public sealed class MonitoringWorkflowTests
{
    [Fact]
    public async Task GetDashboardAsyncAggregatesCallbackIssuesAndCompletedJobs()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedAsync(database.DbContext);
        var workflow = CreateWorkflow(database.DbContext, new OperationsMonitor());

        var dashboard = await workflow.GetDashboardAsync(includeCompletedCampaigns: true);

        Assert.NotEmpty(dashboard.Campaigns);
        var callbackIssues = Assert.Single(dashboard.PersistedIssues, issue => issue.Category == "callback_issues");
        Assert.Equal(3, callbackIssues.Count);
        Assert.Contains("Unmatched: 2, Rejected: 1", callbackIssues.Detail);
        Assert.Contains(dashboard.CompletedJobs, job => job.JobType == "import");
        Assert.Contains(dashboard.CompletedJobs, job => job.JobType == "campaign_send");
    }

    [Fact]
    public async Task GetCampaignDetailAsyncThrowsWhenCampaignDoesNotExist()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var workflow = CreateWorkflow(database.DbContext, new OperationsMonitor());

        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.GetCampaignDetailAsync(999));
    }

    [Fact]
    public async Task ExportHtmlReportAsyncWritesCarrierAndFailureSections()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedAsync(database.DbContext);
        var workflow = CreateWorkflow(database.DbContext, new OperationsMonitor());
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");

        try
        {
            var writtenPath = await workflow.ExportHtmlReportAsync(outputPath);
            var html = await File.ReadAllTextAsync(writtenPath);

            Assert.Equal(Path.GetFullPath(outputPath), writtenPath);
            Assert.Contains("Carrier Footprint", html, StringComparison.Ordinal);
            Assert.Contains("Phone 1", html, StringComparison.Ordinal);
            Assert.Contains("Acme Wireless", html, StringComparison.Ordinal);
            Assert.Contains("Recent Enrichment Failures", html, StringComparison.Ordinal);
            Assert.Contains("Lookup timeout", html, StringComparison.Ordinal);
            Assert.Contains("Monitor Me", html, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static MonitoringWorkflow CreateWorkflow(
        PenelopeSmsDbContext dbContext,
        IOperationsMonitor operationsMonitor)
    {
        return new MonitoringWorkflow(
            new CampaignMonitoringQuery(dbContext),
            new OperationsIssueQuery(dbContext),
            new MonitoringHtmlReportQuery(dbContext),
            operationsMonitor,
            new FakeAwsSqsClient(new SqsQueueDepthSnapshot(12, 2)),
            Options.Create(new AwsOptions
            {
                CallbackQueueUrl = "https://sqs.example.com/queue"
            }));
    }

    private static async Task SeedAsync(PenelopeSmsDbContext dbContext)
    {
        var campaign = new Campaign
        {
            Name = "Monitor Me",
            TemplateFilePath = @"C:\templates\monitor.txt",
            TemplateBody = "Monitor",
            BatchSize = 10,
            Status = CampaignStatus.Completed,
            CreatedAtUtc = new DateTime(2026, 03, 13, 10, 00, 00, DateTimeKind.Utc),
            StartedAtUtc = new DateTime(2026, 03, 13, 10, 05, 00, DateTimeKind.Utc),
            CompletedAtUtc = new DateTime(2026, 03, 13, 10, 30, 00, DateTimeKind.Utc)
        };

        var phoneRecord = new PhoneNumberRecord
        {
            CanonicalPhoneNumber = "+16502530000",
            CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible,
            EnrichmentFailureStatus = EnrichmentFailureStatus.Retryable,
            LastEnrichmentFailedAtUtc = new DateTime(2026, 03, 13, 11, 00, 00, DateTimeKind.Utc),
            LastEnrichmentErrorMessage = "Lookup timeout"
        };
        var enrichedPhoneRecord = new PhoneNumberRecord
        {
            CanonicalPhoneNumber = "+16502530001",
            CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible,
            LastEnrichedAtUtc = new DateTime(2026, 03, 13, 10, 45, 00, DateTimeKind.Utc),
            LastEnrichmentAttemptedAtUtc = new DateTime(2026, 03, 13, 10, 45, 00, DateTimeKind.Utc),
            TwilioLineType = "mobile",
            TwilioCarrierName = "Acme Wireless",
            EligibilityEvaluatedAtUtc = new DateTime(2026, 03, 13, 10, 45, 00, DateTimeKind.Utc)
        };

        dbContext.Campaigns.Add(campaign);
        dbContext.PhoneNumberRecords.AddRange(phoneRecord, enrichedPhoneRecord);
        dbContext.ImportBatches.Add(new ImportBatch
        {
            Id = 1,
            StartedAtUtc = new DateTime(2026, 03, 13, 09, 00, 00, DateTimeKind.Utc),
            CompletedAtUtc = new DateTime(2026, 03, 13, 09, 10, 00, DateTimeKind.Utc),
            Status = ImportBatch.CompletedStatus,
            RowsRead = 10,
            RowsImported = 8,
            RowsRejected = 2
        });
        await dbContext.SaveChangesAsync();

        dbContext.CustomerPhoneLinks.Add(new CustomerPhoneLink
        {
            CustSid = "CUST-1",
            IsVip = false,
            ImportedPhoneSource = ImportedPhoneSource.Phone1,
            RawPhoneNumber = "(650) 253-0001",
            PhoneNumberRecordId = enrichedPhoneRecord.Id,
            ImportBatchId = 1
        });

        dbContext.CampaignRecipients.Add(new CampaignRecipient
        {
            CampaignId = campaign.Id,
            PhoneNumberRecordId = phoneRecord.Id,
            Status = CampaignRecipientStatus.Delivered,
            SubmittedAtUtc = new DateTime(2026, 03, 13, 10, 10, 00, DateTimeKind.Utc),
            CurrentStatusAtUtc = new DateTime(2026, 03, 13, 10, 20, 00, DateTimeKind.Utc),
            LastDeliveryCallbackReceivedAtUtc = new DateTime(2026, 03, 13, 10, 21, 00, DateTimeKind.Utc)
        });
        dbContext.UnmatchedDeliveryCallbacks.AddRange(
            new UnmatchedDeliveryCallback
            {
                TwilioMessageSid = "SM-1",
                MessageStatus = "delivered",
                CallbackFingerprint = "fingerprint-1",
                RawPayloadJson = "{}",
                ReceivedAtUtc = new DateTime(2026, 03, 13, 11, 30, 00, DateTimeKind.Utc),
                FirstSeenAtUtc = new DateTime(2026, 03, 13, 11, 30, 00, DateTimeKind.Utc),
                LastSeenAtUtc = new DateTime(2026, 03, 13, 11, 31, 00, DateTimeKind.Utc)
            },
            new UnmatchedDeliveryCallback
            {
                TwilioMessageSid = "SM-2",
                MessageStatus = "failed",
                CallbackFingerprint = "fingerprint-2",
                RawPayloadJson = "{}",
                ReceivedAtUtc = new DateTime(2026, 03, 13, 11, 32, 00, DateTimeKind.Utc),
                FirstSeenAtUtc = new DateTime(2026, 03, 13, 11, 32, 00, DateTimeKind.Utc),
                LastSeenAtUtc = new DateTime(2026, 03, 13, 11, 33, 00, DateTimeKind.Utc)
            });

        dbContext.RejectedDeliveryCallbacks.Add(new RejectedDeliveryCallback
        {
            RejectionReason = "invalid_signature",
            CallbackFingerprint = "rejected-1",
            RawPayloadJson = "{}",
            ReceivedAtUtc = new DateTime(2026, 03, 13, 11, 35, 00, DateTimeKind.Utc),
            FirstSeenAtUtc = new DateTime(2026, 03, 13, 11, 35, 00, DateTimeKind.Utc),
            LastSeenAtUtc = new DateTime(2026, 03, 13, 11, 36, 00, DateTimeKind.Utc)
        });

        await dbContext.SaveChangesAsync();
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

    private sealed class FakeAwsSqsClient(SqsQueueDepthSnapshot depthSnapshot) : IAwsSqsClient
    {
        public Task<SqsQueueMessage?> ReceiveMessageAsync(string queueUrl, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SqsQueueMessage?>(null);
        }

        public Task<SqsQueueDepthSnapshot> GetQueueDepthAsync(string queueUrl, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(depthSnapshot);
        }

        public Task DeleteMessageAsync(string queueUrl, string receiptHandle, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
