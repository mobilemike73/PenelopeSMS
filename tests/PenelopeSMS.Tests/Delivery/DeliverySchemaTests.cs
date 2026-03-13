using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.SqlServer;

namespace PenelopeSMS.Tests.Delivery;

public sealed class DeliverySchemaTests
{
    [Fact]
    public async Task CampaignRecipientStatusHistoryUsesFingerprintUniqueness()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var recipient = await SeedRecipientAsync(database.DbContext);

        database.DbContext.CampaignRecipientStatusHistory.AddRange(
            new CampaignRecipientStatusHistory
            {
                CampaignRecipientId = recipient.Id,
                Status = CampaignRecipientStatus.Delivered,
                ProviderEventAtUtc = DateTime.UtcNow,
                EventTimeSource = DeliveryEventTimeSource.CallbackReceivedAt,
                CallbackFingerprint = "fingerprint-1",
                RawPayloadJson = "{}",
                ReceivedAtUtc = DateTime.UtcNow
            },
            new CampaignRecipientStatusHistory
            {
                CampaignRecipientId = recipient.Id,
                Status = CampaignRecipientStatus.Delivered,
                ProviderEventAtUtc = DateTime.UtcNow,
                EventTimeSource = DeliveryEventTimeSource.CallbackReceivedAt,
                CallbackFingerprint = "fingerprint-1",
                RawPayloadJson = "{}",
                ReceivedAtUtc = DateTime.UtcNow
            });

        await Assert.ThrowsAsync<DbUpdateException>(() => database.DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task CampaignRecipientCanPersistCurrentDeliveryMetadata()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var recipient = await SeedRecipientAsync(database.DbContext);
        var eventAtUtc = DateTime.UtcNow;

        recipient.Status = CampaignRecipientStatus.Delivered;
        recipient.CurrentStatusAtUtc = eventAtUtc;
        recipient.CurrentStatusTimeSource = DeliveryEventTimeSource.CallbackReceivedAt;
        recipient.CurrentStatusRawValue = "callback-received";
        recipient.LastDeliveryCallbackReceivedAtUtc = eventAtUtc;
        recipient.DeliveryErrorCode = "30001";
        recipient.DeliveryErrorMessage = "Carrier delivery failure.";

        await database.DbContext.SaveChangesAsync();

        var storedRecipient = await database.DbContext.CampaignRecipients.SingleAsync();

        Assert.Equal(CampaignRecipientStatus.Delivered, storedRecipient.Status);
        Assert.Equal(eventAtUtc, storedRecipient.CurrentStatusAtUtc);
        Assert.Equal(DeliveryEventTimeSource.CallbackReceivedAt, storedRecipient.CurrentStatusTimeSource);
        Assert.Equal("callback-received", storedRecipient.CurrentStatusRawValue);
        Assert.Equal("30001", storedRecipient.DeliveryErrorCode);
        Assert.Equal("Carrier delivery failure.", storedRecipient.DeliveryErrorMessage);
    }

    private static async Task<CampaignRecipient> SeedRecipientAsync(PenelopeSmsDbContext dbContext)
    {
        var campaign = new Campaign
        {
            Name = "Delivery Pipeline",
            TemplateFilePath = @"C:\templates\delivery.txt",
            TemplateBody = "Hi",
            BatchSize = 10
        };

        var phoneNumberRecord = new PhoneNumberRecord
        {
            CanonicalPhoneNumber = "+16502530000"
        };

        var recipient = new CampaignRecipient
        {
            Campaign = campaign,
            PhoneNumberRecord = phoneNumberRecord,
            Status = CampaignRecipientStatus.Submitted,
            TwilioMessageSid = "SM123"
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
