using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenelopeSMS.App.Monitoring;
using PenelopeSMS.App.Rendering;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.SqlServer;
using PenelopeSMS.Infrastructure.SqlServer.Queries;

namespace PenelopeSMS.Tests.Monitoring;

public sealed class MonitoringRuntimeIntegrationTests
{
    [Fact]
    public async Task GetDashboardAsyncCombinesPersistedAndLiveMonitoringState()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedAsync(database.DbContext);
        var monitor = new OperationsMonitor();
        var startedAtUtc = new DateTime(2026, 03, 13, 12, 15, 00, DateTimeKind.Utc);
        var completedAtUtc = new DateTime(2026, 03, 13, 12, 20, 00, DateTimeKind.Utc);
        var warningAtUtc = new DateTime(2026, 03, 13, 12, 18, 00, DateTimeKind.Utc);

        monitor.StartJob(
            OperationType.DeliveryProcessing,
            "Delivery callback processing",
            "Waiting for queue messages",
            startedAtUtc);
        var enrichmentJobId = monitor.StartJob(
            OperationType.Enrichment,
            "Due-record enrichment",
            "Processing selected records",
            startedAtUtc);
        monitor.CompleteJob(
            enrichmentJobId,
            "Enrichment complete. Selected: 3, Processed: 3, Updated: 2, Failed: 1",
            completedAtUtc);
        monitor.Warn(
            OperationType.Import,
            "Oracle import is retrying after timeout.",
            createdAtUtc: warningAtUtc);
        monitor.RecordLiveDeliveryLine(
            "Deleted queue message message-1 after applied.",
            completedAtUtc);

        var workflow = new MonitoringWorkflow(
            new CampaignMonitoringQuery(database.DbContext),
            new OperationsIssueQuery(database.DbContext),
            new MonitoringHtmlReportQuery(database.DbContext),
            monitor);

        var dashboard = await workflow.GetDashboardAsync(includeCompletedCampaigns: true);

        Assert.Contains(dashboard.ActiveJobsOrEmpty, job => job.JobType == "Delivery processing");
        Assert.Contains(dashboard.ActiveWarningsOrEmpty, warning => warning.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dashboard.LiveDeliveryLinesOrEmpty, line => line.Contains("message-1", StringComparison.Ordinal));
        Assert.Contains(dashboard.CompletedJobs, job => job.JobType == "enrichment" && job.IsLiveSession);
        Assert.Contains(dashboard.CompletedJobs, job => job.JobType == "import" && !job.IsLiveSession);
        Assert.Contains(dashboard.CompletedJobs, job => job.JobType == "campaign_send" && !job.IsLiveSession);
    }

    private static async Task SeedAsync(PenelopeSmsDbContext dbContext)
    {
        var campaign = new Campaign
        {
            Name = "Monitor Runtime",
            TemplateFilePath = @"C:\templates\runtime.txt",
            TemplateBody = "Runtime",
            BatchSize = 5,
            Status = CampaignStatus.Sending,
            CreatedAtUtc = new DateTime(2026, 03, 13, 10, 00, 00, DateTimeKind.Utc),
            StartedAtUtc = new DateTime(2026, 03, 13, 10, 05, 00, DateTimeKind.Utc)
        };

        var phoneRecord = new PhoneNumberRecord
        {
            CanonicalPhoneNumber = "+16502530000",
            CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible
        };

        dbContext.Campaigns.Add(campaign);
        dbContext.PhoneNumberRecords.Add(phoneRecord);
        await dbContext.SaveChangesAsync();

        dbContext.CampaignRecipients.Add(new CampaignRecipient
        {
            CampaignId = campaign.Id,
            PhoneNumberRecordId = phoneRecord.Id,
            Status = CampaignRecipientStatus.Submitted,
            SubmittedAtUtc = new DateTime(2026, 03, 13, 10, 10, 00, DateTimeKind.Utc)
        });

        dbContext.ImportBatches.Add(new ImportBatch
        {
            StartedAtUtc = new DateTime(2026, 03, 13, 09, 00, 00, DateTimeKind.Utc),
            CompletedAtUtc = new DateTime(2026, 03, 13, 09, 10, 00, DateTimeKind.Utc),
            Status = ImportBatch.CompletedStatus,
            RowsRead = 10,
            RowsImported = 9,
            RowsRejected = 1
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
