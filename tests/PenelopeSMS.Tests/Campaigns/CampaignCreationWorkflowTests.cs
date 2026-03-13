using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenelopeSMS.App.Templates;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.SqlServer;
using PenelopeSMS.Infrastructure.SqlServer.Queries;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;

namespace PenelopeSMS.Tests.Campaigns;

public sealed class CampaignCreationWorkflowTests
{
    private static readonly int[] ExpectedStandardEligiblePhoneRecordIds = [1, 3];
    private static readonly int[] ExpectedVipEligiblePhoneRecordIds = [4];

    [Fact]
    public async Task CreateDraftAsyncCreatesCampaignFromTemplateAndEligibleCanonicalRecipientsOnly()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedPhoneNumbersAsync(database.DbContext);

        var workflow = new CampaignCreationWorkflow(
            new FakePlainTextTemplateLoader(@"C:\templates\march.txt", "Hello from PenelopeSMS"),
            new CampaignRecipientSelectionQuery(database.DbContext),
            new CampaignRepository(database.DbContext));

        var result = await workflow.CreateDraftAsync(@"C:\templates\march.txt", 250);

        var storedCampaign = await database.DbContext.Campaigns
            .Include(campaign => campaign.Recipients)
            .SingleAsync();
        var storedRecipients = await database.DbContext.CampaignRecipients
            .OrderBy(recipient => recipient.PhoneNumberRecordId)
            .ToListAsync();

        Assert.Equal("march", result.CampaignName);
        Assert.Equal(250, result.BatchSize);
        Assert.Equal(2, result.DraftedRecipients);
        Assert.Equal(1, result.SkippedIneligibleRecipients);
        Assert.Equal(@"C:\templates\march.txt", storedCampaign.TemplateFilePath);
        Assert.Equal("Hello from PenelopeSMS", storedCampaign.TemplateBody);
        Assert.Equal(250, storedCampaign.BatchSize);
        Assert.Equal(CustomerSegment.Standard, storedCampaign.AudienceSegment);
        Assert.Equal(2, storedCampaign.Recipients.Count);
        Assert.Equal(2, storedRecipients.Count);
        Assert.All(storedRecipients, recipient => Assert.Equal(CampaignRecipientStatus.Pending, recipient.Status));
    }

    [Fact]
    public async Task CreateDraftAsyncStoresOneRecipientPerEligibleCanonicalPhone()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedPhoneNumbersAsync(database.DbContext);

        var workflow = new CampaignCreationWorkflow(
            new FakePlainTextTemplateLoader(@"C:\templates\april.txt", "April update"),
            new CampaignRecipientSelectionQuery(database.DbContext),
            new CampaignRepository(database.DbContext));

        await workflow.CreateDraftAsync(@"C:\templates\april.txt", 100);

        var campaignRecipients = await database.DbContext.CampaignRecipients
            .OrderBy(recipient => recipient.PhoneNumberRecordId)
            .ToListAsync();

        Assert.Equal(2, campaignRecipients.Count);
        Assert.Equal(
            ExpectedStandardEligiblePhoneRecordIds,
            campaignRecipients.Select(recipient => recipient.PhoneNumberRecordId));
    }

    [Fact]
    public async Task CreateDraftAsyncCanTargetVipRecipientsOnly()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedPhoneNumbersAsync(database.DbContext);

        var workflow = new CampaignCreationWorkflow(
            new FakePlainTextTemplateLoader(@"C:\templates\vip.txt", "VIP update"),
            new CampaignRecipientSelectionQuery(database.DbContext),
            new CampaignRepository(database.DbContext));

        var result = await workflow.CreateDraftAsync(
            @"C:\templates\vip.txt",
            50,
            CustomerSegment.Vip);

        var storedCampaign = await database.DbContext.Campaigns
            .Include(campaign => campaign.Recipients)
            .SingleAsync(campaign => campaign.Id == result.CampaignId);

        Assert.Equal(CustomerSegment.Vip, result.AudienceSegment);
        Assert.Equal(1, result.DraftedRecipients);
        Assert.Equal(0, result.SkippedIneligibleRecipients);
        Assert.Equal(CustomerSegment.Vip, storedCampaign.AudienceSegment);
        Assert.Equal(
            ExpectedVipEligiblePhoneRecordIds,
            storedCampaign.Recipients.Select(recipient => recipient.PhoneNumberRecordId).OrderBy(id => id));
    }

    private static async Task SeedPhoneNumbersAsync(PenelopeSmsDbContext dbContext)
    {
        var importBatch = new ImportBatch
        {
            Status = ImportBatch.CompletedStatus,
            RowsRead = 4,
            RowsImported = 3,
            RowsRejected = 1,
            CompletedAtUtc = DateTime.UtcNow
        };

        var eligiblePrimary = new PhoneNumberRecord
        {
            CanonicalPhoneNumber = "+16502530000",
            CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible
        };

        var ineligible = new PhoneNumberRecord
        {
            CanonicalPhoneNumber = "+16502530001",
            CampaignEligibilityStatus = CampaignEligibilityStatus.Ineligible
        };

        var eligibleSecondary = new PhoneNumberRecord
        {
            CanonicalPhoneNumber = "+16502530002",
            CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible
        };

        var eligibleVip = new PhoneNumberRecord
        {
            CanonicalPhoneNumber = "+16502530003",
            CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible
        };

        dbContext.ImportBatches.Add(importBatch);
        dbContext.PhoneNumberRecords.AddRange(eligiblePrimary, ineligible, eligibleSecondary, eligibleVip);
        await dbContext.SaveChangesAsync();

        dbContext.CustomerPhoneLinks.AddRange(
            new CustomerPhoneLink
            {
                CustSid = "CUST-001",
                IsVip = false,
                ImportedPhoneSource = ImportedPhoneSource.Phone1,
                RawPhoneNumber = "650-253-0000",
                ImportBatchId = importBatch.Id,
                PhoneNumberRecordId = eligiblePrimary.Id
            },
            new CustomerPhoneLink
            {
                CustSid = "CUST-002",
                IsVip = false,
                ImportedPhoneSource = ImportedPhoneSource.Phone2,
                RawPhoneNumber = "+1 (650) 253-0000",
                ImportBatchId = importBatch.Id,
                PhoneNumberRecordId = eligiblePrimary.Id
            },
            new CustomerPhoneLink
            {
                CustSid = "CUST-003",
                IsVip = false,
                ImportedPhoneSource = ImportedPhoneSource.Phone1,
                RawPhoneNumber = "650-253-0002",
                ImportBatchId = importBatch.Id,
                PhoneNumberRecordId = eligibleSecondary.Id
            },
            new CustomerPhoneLink
            {
                CustSid = "CUST-004",
                IsVip = false,
                ImportedPhoneSource = ImportedPhoneSource.Phone1,
                RawPhoneNumber = "650-253-0001",
                ImportBatchId = importBatch.Id,
                PhoneNumberRecordId = ineligible.Id
            },
            new CustomerPhoneLink
            {
                CustSid = "CUST-005",
                IsVip = true,
                ImportedPhoneSource = ImportedPhoneSource.Phone1,
                RawPhoneNumber = "650-253-0003",
                ImportBatchId = importBatch.Id,
                PhoneNumberRecordId = eligibleVip.Id
            });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakePlainTextTemplateLoader(string expectedPath, string templateBody) : IPlainTextTemplateLoader
    {
        public Task<PlainTextTemplateLoadResult> LoadAsync(
            string templatePath,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(expectedPath, templatePath);
            return Task.FromResult(new PlainTextTemplateLoadResult(templatePath, templateBody));
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
