using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.SqlServer;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;

namespace PenelopeSMS.Tests.Delivery;

public sealed class DeliveryCallbackRepositoryTests
{
    [Fact]
    public async Task ApplyAsyncCollapsesDuplicateCallbacksByFingerprint()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedRecipientAsync(database.DbContext);
        var repository = new DeliveryCallbackRepository(database.DbContext);
        var receivedAtUtc = DateTime.UtcNow;

        var envelope = new DeliveryCallbackEnvelopeMessage(
            MessageSid: "SM123",
            MessageStatus: "delivered",
            ProviderErrorCode: null,
            ProviderErrorMessage: null,
            ProviderEventRawValue: null,
            RawPayloadJson: "{\"Body\":\"one\"}",
            ReceivedAtUtc: receivedAtUtc);

        await repository.ApplyAsync(envelope);
        var duplicateResult = await repository.ApplyAsync(envelope with { ReceivedAtUtc = receivedAtUtc.AddMinutes(1) });

        var historyEntries = await database.DbContext.CampaignRecipientStatusHistory.ToListAsync();

        Assert.Equal("duplicate", duplicateResult.Outcome);
        Assert.Single(historyEntries);
        Assert.Equal(receivedAtUtc.AddMinutes(1), historyEntries[0].LastSeenAtUtc);
    }

    [Fact]
    public async Task ApplyAsyncDiscardsOlderEventsWithoutChangingCurrentState()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var recipient = await SeedRecipientAsync(database.DbContext);
        var currentStatusAtUtc = new DateTime(2026, 03, 13, 12, 05, 00, DateTimeKind.Utc);
        var receivedAtUtc = currentStatusAtUtc.AddMinutes(2);
        recipient.Status = CampaignRecipientStatus.Delivered;
        recipient.CurrentStatusAtUtc = currentStatusAtUtc;
        recipient.LastDeliveryCallbackReceivedAtUtc = currentStatusAtUtc;
        await database.DbContext.SaveChangesAsync();

        var repository = new DeliveryCallbackRepository(database.DbContext);

        var result = await repository.ApplyAsync(new DeliveryCallbackEnvelopeMessage(
            MessageSid: "SM123",
            MessageStatus: "sent",
            ProviderErrorCode: null,
            ProviderErrorMessage: null,
            ProviderEventRawValue: null,
            RawPayloadJson: "{\"Body\":\"older\"}",
            ReceivedAtUtc: receivedAtUtc));

        var storedRecipient = await database.DbContext.CampaignRecipients.SingleAsync();

        Assert.Equal("older_discarded", result.Outcome);
        Assert.Equal(CampaignRecipientStatus.Delivered, storedRecipient.Status);
        Assert.Equal(currentStatusAtUtc, storedRecipient.CurrentStatusAtUtc);
        Assert.Equal(receivedAtUtc, storedRecipient.LastDeliveryCallbackReceivedAtUtc);
        Assert.Empty(await database.DbContext.CampaignRecipientStatusHistory.ToListAsync());
    }

    [Fact]
    public async Task ApplyAsyncStoresUnknownMessageSidInUnmatchedCallbacks()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = new DeliveryCallbackRepository(database.DbContext);

        var result = await repository.ApplyAsync(new DeliveryCallbackEnvelopeMessage(
            MessageSid: "SM-UNKNOWN",
            MessageStatus: "delivered",
            ProviderErrorCode: "30001",
            ProviderErrorMessage: "Carrier failure",
            ProviderEventRawValue: null,
            RawPayloadJson: "{\"Body\":\"unknown\"}",
            ReceivedAtUtc: DateTime.UtcNow));

        var unmatchedCallbacks = await database.DbContext.UnmatchedDeliveryCallbacks.ToListAsync();

        Assert.Equal("unmatched", result.Outcome);
        Assert.Single(unmatchedCallbacks);
        Assert.Equal("SM-UNKNOWN", unmatchedCallbacks[0].TwilioMessageSid);
    }

    [Fact]
    public async Task ApplyAsyncUpdatesLastSeenAtUtcForDuplicateUnmatchedCallbacks()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = new DeliveryCallbackRepository(database.DbContext);
        var firstSeenAtUtc = new DateTime(2026, 03, 13, 13, 00, 00, DateTimeKind.Utc);
        var lastSeenAtUtc = firstSeenAtUtc.AddMinutes(3);

        var envelope = new DeliveryCallbackEnvelopeMessage(
            MessageSid: "SM-UNKNOWN",
            MessageStatus: "failed",
            ProviderErrorCode: "30002",
            ProviderErrorMessage: "Unknown destination",
            ProviderEventRawValue: null,
            RawPayloadJson: "{\"Body\":\"unknown\"}",
            ReceivedAtUtc: firstSeenAtUtc);

        await repository.ApplyAsync(envelope);
        await repository.ApplyAsync(envelope with { ReceivedAtUtc = lastSeenAtUtc });

        var unmatchedCallback = await database.DbContext.UnmatchedDeliveryCallbacks.SingleAsync();

        Assert.Equal(firstSeenAtUtc, unmatchedCallback.FirstSeenAtUtc);
        Assert.Equal(lastSeenAtUtc, unmatchedCallback.LastSeenAtUtc);
    }

    private static async Task<CampaignRecipient> SeedRecipientAsync(PenelopeSmsDbContext dbContext)
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
