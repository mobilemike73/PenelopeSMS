using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.SqlServer;

namespace PenelopeSMS.Tests.Campaigns;

public sealed class CampaignSchemaTests
{
    [Fact]
    public async Task CampaignRecipientUniquenessPreventsDuplicateCanonicalPhonesInOneCampaign()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var phoneNumber = new PhoneNumberRecord
        {
            CanonicalPhoneNumber = "+16502530000"
        };

        var campaign = new Campaign
        {
            Name = "March Promo",
            TemplateFilePath = @"C:\templates\march.txt",
            TemplateBody = "Hello from PenelopeSMS",
            BatchSize = 250,
            AudienceSegment = CustomerSegment.Vip
        };

        database.DbContext.PhoneNumberRecords.Add(phoneNumber);
        database.DbContext.Campaigns.Add(campaign);
        await database.DbContext.SaveChangesAsync();

        database.DbContext.CampaignRecipients.AddRange(
            new CampaignRecipient
            {
                CampaignId = campaign.Id,
                PhoneNumberRecordId = phoneNumber.Id
            },
            new CampaignRecipient
            {
                CampaignId = campaign.Id,
                PhoneNumberRecordId = phoneNumber.Id
            });

        await Assert.ThrowsAsync<DbUpdateException>(() => database.DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task CampaignPersistsTemplateSnapshotAndBatchSize()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        database.DbContext.Campaigns.Add(new Campaign
        {
            Name = "Spring Promo",
            TemplateFilePath = @"C:\templates\spring.txt",
            TemplateBody = "Line one\nLine two",
            BatchSize = 150,
            AudienceSegment = CustomerSegment.Standard
        });

        await database.DbContext.SaveChangesAsync();

        var storedCampaign = await database.DbContext.Campaigns.SingleAsync();

        Assert.Equal("Spring Promo", storedCampaign.Name);
        Assert.Equal(@"C:\templates\spring.txt", storedCampaign.TemplateFilePath);
        Assert.Equal("Line one\nLine two", storedCampaign.TemplateBody);
        Assert.Equal(150, storedCampaign.BatchSize);
        Assert.Equal(CustomerSegment.Standard, storedCampaign.AudienceSegment);
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
