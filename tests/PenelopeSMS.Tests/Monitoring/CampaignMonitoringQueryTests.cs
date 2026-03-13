using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.SqlServer;
using PenelopeSMS.Infrastructure.SqlServer.Queries;

namespace PenelopeSMS.Tests.Monitoring;

public sealed class CampaignMonitoringQueryTests
{
    [Fact]
    public async Task ListCampaignsAsyncReturnsAllStatusCountsAndSortsByRecentActivity()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedCampaignsAsync(database.DbContext);
        var query = new CampaignMonitoringQuery(database.DbContext);

        var campaigns = await query.ListCampaignsAsync();

        Assert.Equal(2, campaigns.Count);
        Assert.Equal("Recent Activity", campaigns[0].CampaignName);
        Assert.Equal(1, campaigns[0].PendingRecipients);
        Assert.Equal(1, campaigns[0].SubmittedRecipients);
        Assert.Equal(1, campaigns[0].QueuedRecipients);
        Assert.Equal(1, campaigns[0].SentRecipients);
        Assert.Equal(1, campaigns[0].DeliveredRecipients);
        Assert.Equal(1, campaigns[0].UndeliveredRecipients);
        Assert.Equal(1, campaigns[0].FailedRecipients);
        Assert.Equal("Older Activity", campaigns[1].CampaignName);
    }

    [Fact]
    public async Task ListCampaignsAsyncHidesCompletedByDefaultAndCanIncludeThem()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedCampaignsAsync(database.DbContext);
        var query = new CampaignMonitoringQuery(database.DbContext);

        var defaultCampaigns = await query.ListCampaignsAsync();
        var includingCompleted = await query.ListCampaignsAsync(includeCompleted: true);

        Assert.DoesNotContain(defaultCampaigns, campaign => campaign.Status == CampaignStatus.Completed);
        Assert.Contains(includingCompleted, campaign => campaign.Status == CampaignStatus.Completed);
    }

    [Fact]
    public async Task GetCampaignDetailAsyncReturnsRecentRecipientIssues()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var campaignId = await SeedCampaignsAsync(database.DbContext);
        var query = new CampaignMonitoringQuery(database.DbContext);

        var detail = await query.GetCampaignDetailAsync(campaignId);

        Assert.NotNull(detail);
        Assert.Equal("Recent Activity", detail!.Summary.CampaignName);
        Assert.NotEmpty(detail.RecentIssues);
        Assert.Contains(detail.RecentIssues, issue => issue.Status == CampaignRecipientStatus.Undelivered);
    }

    private static async Task<int> SeedCampaignsAsync(PenelopeSmsDbContext dbContext)
    {
        var recentCampaign = new Campaign
        {
            Name = "Recent Activity",
            TemplateFilePath = @"C:\templates\recent.txt",
            TemplateBody = "Recent",
            BatchSize = 5,
            Status = CampaignStatus.Sending,
            CreatedAtUtc = new DateTime(2026, 03, 13, 12, 00, 00, DateTimeKind.Utc),
            StartedAtUtc = new DateTime(2026, 03, 13, 12, 05, 00, DateTimeKind.Utc)
        };

        var olderCampaign = new Campaign
        {
            Name = "Older Activity",
            TemplateFilePath = @"C:\templates\older.txt",
            TemplateBody = "Older",
            BatchSize = 5,
            Status = CampaignStatus.Sending,
            CreatedAtUtc = new DateTime(2026, 03, 13, 08, 00, 00, DateTimeKind.Utc),
            StartedAtUtc = new DateTime(2026, 03, 13, 08, 05, 00, DateTimeKind.Utc)
        };

        var completedCampaign = new Campaign
        {
            Name = "Completed Activity",
            TemplateFilePath = @"C:\templates\complete.txt",
            TemplateBody = "Complete",
            BatchSize = 5,
            Status = CampaignStatus.Completed,
            CreatedAtUtc = new DateTime(2026, 03, 12, 08, 00, 00, DateTimeKind.Utc),
            StartedAtUtc = new DateTime(2026, 03, 12, 08, 05, 00, DateTimeKind.Utc),
            CompletedAtUtc = new DateTime(2026, 03, 12, 09, 00, 00, DateTimeKind.Utc)
        };

        dbContext.Campaigns.AddRange(recentCampaign, olderCampaign, completedCampaign);

        var numbers = Enumerable.Range(0, 10)
            .Select(index => new PhoneNumberRecord
            {
                CanonicalPhoneNumber = $"+16502530{index:000}",
                CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible
            })
            .ToArray();

        dbContext.PhoneNumberRecords.AddRange(numbers);
        await dbContext.SaveChangesAsync();

        dbContext.CampaignRecipients.AddRange(
            BuildRecipient(recentCampaign.Id, numbers[0].Id, CampaignRecipientStatus.Pending, createdAtUtc: new DateTime(2026, 03, 13, 12, 09, 00, DateTimeKind.Utc)),
            BuildRecipient(recentCampaign.Id, numbers[1].Id, CampaignRecipientStatus.Submitted, submittedAtUtc: new DateTime(2026, 03, 13, 12, 10, 00, DateTimeKind.Utc)),
            BuildRecipient(recentCampaign.Id, numbers[2].Id, CampaignRecipientStatus.Queued, currentStatusAtUtc: new DateTime(2026, 03, 13, 12, 11, 00, DateTimeKind.Utc), createdAtUtc: new DateTime(2026, 03, 13, 12, 11, 00, DateTimeKind.Utc)),
            BuildRecipient(recentCampaign.Id, numbers[3].Id, CampaignRecipientStatus.Sent, currentStatusAtUtc: new DateTime(2026, 03, 13, 12, 12, 00, DateTimeKind.Utc), createdAtUtc: new DateTime(2026, 03, 13, 12, 12, 00, DateTimeKind.Utc)),
            BuildRecipient(recentCampaign.Id, numbers[4].Id, CampaignRecipientStatus.Delivered, currentStatusAtUtc: new DateTime(2026, 03, 13, 12, 20, 00, DateTimeKind.Utc), createdAtUtc: new DateTime(2026, 03, 13, 12, 20, 00, DateTimeKind.Utc)),
            BuildRecipient(recentCampaign.Id, numbers[5].Id, CampaignRecipientStatus.Undelivered, currentStatusAtUtc: new DateTime(2026, 03, 13, 12, 30, 00, DateTimeKind.Utc), createdAtUtc: new DateTime(2026, 03, 13, 12, 30, 00, DateTimeKind.Utc), deliveryErrorMessage: "Carrier blocked"),
            BuildRecipient(recentCampaign.Id, numbers[6].Id, CampaignRecipientStatus.Failed, lastAttemptedAtUtc: new DateTime(2026, 03, 13, 12, 15, 00, DateTimeKind.Utc), createdAtUtc: new DateTime(2026, 03, 13, 12, 15, 00, DateTimeKind.Utc), providerErrorMessage: "Immediate failure"),
            BuildRecipient(olderCampaign.Id, numbers[7].Id, CampaignRecipientStatus.Delivered, currentStatusAtUtc: new DateTime(2026, 03, 13, 08, 15, 00, DateTimeKind.Utc), createdAtUtc: new DateTime(2026, 03, 13, 08, 15, 00, DateTimeKind.Utc)),
            BuildRecipient(completedCampaign.Id, numbers[8].Id, CampaignRecipientStatus.Delivered, currentStatusAtUtc: new DateTime(2026, 03, 12, 08, 30, 00, DateTimeKind.Utc), createdAtUtc: new DateTime(2026, 03, 12, 08, 30, 00, DateTimeKind.Utc)),
            BuildRecipient(completedCampaign.Id, numbers[9].Id, CampaignRecipientStatus.Delivered, currentStatusAtUtc: new DateTime(2026, 03, 12, 08, 40, 00, DateTimeKind.Utc), createdAtUtc: new DateTime(2026, 03, 12, 08, 40, 00, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();
        return recentCampaign.Id;
    }

    private static CampaignRecipient BuildRecipient(
        int campaignId,
        int phoneNumberRecordId,
        CampaignRecipientStatus status,
        DateTime? currentStatusAtUtc = null,
        DateTime? submittedAtUtc = null,
        DateTime? lastAttemptedAtUtc = null,
        DateTime? createdAtUtc = null,
        string? providerErrorMessage = null,
        string? deliveryErrorMessage = null)
    {
        return new CampaignRecipient
        {
            CampaignId = campaignId,
            PhoneNumberRecordId = phoneNumberRecordId,
            Status = status,
            CreatedAtUtc = createdAtUtc ?? currentStatusAtUtc ?? submittedAtUtc ?? lastAttemptedAtUtc ?? DateTime.UtcNow,
            SubmittedAtUtc = submittedAtUtc,
            LastAttemptedAtUtc = lastAttemptedAtUtc,
            CurrentStatusAtUtc = currentStatusAtUtc,
            LastDeliveryCallbackReceivedAtUtc = currentStatusAtUtc,
            ProviderErrorMessage = providerErrorMessage,
            DeliveryErrorMessage = deliveryErrorMessage
        };
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
