using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.SqlServer;
using PenelopeSMS.Infrastructure.SqlServer.Queries;

namespace PenelopeSMS.Tests.Monitoring;

public sealed class MonitoringWorkflowTests
{
    [Fact]
    public async Task GetDashboardAsyncAggregatesCallbackIssuesAndCompletedJobs()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedAsync(database.DbContext);
        var workflow = CreateWorkflow(database.DbContext);

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
        var workflow = CreateWorkflow(database.DbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.GetCampaignDetailAsync(999));
    }

    private static MonitoringWorkflow CreateWorkflow(PenelopeSmsDbContext dbContext)
    {
        return new MonitoringWorkflow(
            new CampaignMonitoringQuery(dbContext),
            new OperationsIssueQuery(dbContext));
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

        dbContext.Campaigns.Add(campaign);
        dbContext.PhoneNumberRecords.Add(phoneRecord);
        await dbContext.SaveChangesAsync();

        dbContext.CampaignRecipients.Add(new CampaignRecipient
        {
            CampaignId = campaign.Id,
            PhoneNumberRecordId = phoneRecord.Id,
            Status = CampaignRecipientStatus.Delivered,
            SubmittedAtUtc = new DateTime(2026, 03, 13, 10, 10, 00, DateTimeKind.Utc),
            CurrentStatusAtUtc = new DateTime(2026, 03, 13, 10, 20, 00, DateTimeKind.Utc),
            LastDeliveryCallbackReceivedAtUtc = new DateTime(2026, 03, 13, 10, 21, 00, DateTimeKind.Utc)
        });

        dbContext.ImportBatches.Add(new ImportBatch
        {
            StartedAtUtc = new DateTime(2026, 03, 13, 09, 00, 00, DateTimeKind.Utc),
            CompletedAtUtc = new DateTime(2026, 03, 13, 09, 10, 00, DateTimeKind.Utc),
            Status = ImportBatch.CompletedStatus,
            RowsRead = 10,
            RowsImported = 8,
            RowsRejected = 2
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
}
