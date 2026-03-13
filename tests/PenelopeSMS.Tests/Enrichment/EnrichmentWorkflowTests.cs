using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.SqlServer;
using PenelopeSMS.Infrastructure.SqlServer.Queries;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;
using PenelopeSMS.Infrastructure.Twilio;

namespace PenelopeSMS.Tests.Enrichment;

public sealed class EnrichmentWorkflowTests
{
    [Fact]
    public async Task RunAsyncDefaultModeTargetsNeverEnrichedFailedAndStaleRecords()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var utcNow = DateTime.UtcNow;
        var neverEnriched = CreatePhoneNumberRecord("+16502530000");
        var failedRecord = CreatePhoneNumberRecord("+16502530001");
        failedRecord.LastEnrichedAtUtc = utcNow.AddDays(-2);
        failedRecord.EnrichmentFailureStatus = EnrichmentFailureStatus.Retryable;
        failedRecord.LastEnrichmentFailedAtUtc = utcNow.AddHours(-1);
        var staleSuccess = CreatePhoneNumberRecord("+16502530002");
        staleSuccess.LastEnrichedAtUtc = utcNow.AddDays(-31);
        staleSuccess.CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible;
        var freshSuccess = CreatePhoneNumberRecord("+16502530003");
        freshSuccess.LastEnrichedAtUtc = utcNow.AddDays(-5);
        freshSuccess.CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible;

        await database.DbContext.PhoneNumberRecords.AddRangeAsync(
            neverEnriched,
            failedRecord,
            staleSuccess,
            freshSuccess);
        await database.DbContext.SaveChangesAsync();

        var lookupClient = new FakeTwilioLookupClient(
            SuccessfulLookupResults(
                neverEnriched.CanonicalPhoneNumber,
                failedRecord.CanonicalPhoneNumber,
                staleSuccess.CanonicalPhoneNumber));
        var workflow = CreateWorkflow(database.DbContext, lookupClient);

        var result = await workflow.RunAsync();

        Assert.False(result.FullRefresh);
        Assert.Equal(3, result.SelectedRecords);
        Assert.Equal(3, result.ProcessedRecords);
        Assert.Equal(3, result.UpdatedRecords);
        Assert.Equal(0, result.FailedRecords);
        Assert.Equal(1, result.SkippedRecords);
        Assert.Equal(
            [neverEnriched.CanonicalPhoneNumber, failedRecord.CanonicalPhoneNumber, staleSuccess.CanonicalPhoneNumber],
            lookupClient.RequestedNumbers);
    }

    [Fact]
    public async Task RunAsyncFullRefreshTargetsAllImportedRecords()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var freshSuccess = CreatePhoneNumberRecord("+16502530010");
        freshSuccess.LastEnrichedAtUtc = DateTime.UtcNow.AddDays(-1);
        freshSuccess.CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible;
        var neverEnriched = CreatePhoneNumberRecord("+16502530011");

        await database.DbContext.PhoneNumberRecords.AddRangeAsync(freshSuccess, neverEnriched);
        await database.DbContext.SaveChangesAsync();

        var lookupClient = new FakeTwilioLookupClient(
            SuccessfulLookupResults(
                freshSuccess.CanonicalPhoneNumber,
                neverEnriched.CanonicalPhoneNumber));
        var workflow = CreateWorkflow(database.DbContext, lookupClient);

        var result = await workflow.RunAsync(fullRefresh: true);

        Assert.True(result.FullRefresh);
        Assert.Equal(2, result.SelectedRecords);
        Assert.Equal(2, result.ProcessedRecords);
        Assert.Equal(2, result.UpdatedRecords);
        Assert.Equal(0, result.FailedRecords);
        Assert.Equal(0, result.SkippedRecords);
        Assert.Equal(
            [freshSuccess.CanonicalPhoneNumber, neverEnriched.CanonicalPhoneNumber],
            lookupClient.RequestedNumbers);
    }

    [Fact]
    public async Task RunAsyncVipModeTargetsOnlyVipRecords()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var importBatch = new ImportBatch
        {
            Status = ImportBatch.CompletedStatus,
            CompletedAtUtc = DateTime.UtcNow
        };
        var vipRecord = CreatePhoneNumberRecord("+16502530100");
        var standardRecord = CreatePhoneNumberRecord("+16502530101");

        database.DbContext.ImportBatches.Add(importBatch);
        database.DbContext.PhoneNumberRecords.AddRange(vipRecord, standardRecord);
        await database.DbContext.SaveChangesAsync();

        database.DbContext.CustomerPhoneLinks.AddRange(
            new CustomerPhoneLink
            {
                CustSid = "VIP-001",
                IsVip = true,
                ImportedPhoneSource = ImportedPhoneSource.Phone1,
                RawPhoneNumber = "6502530100",
                ImportBatchId = importBatch.Id,
                PhoneNumberRecordId = vipRecord.Id
            },
            new CustomerPhoneLink
            {
                CustSid = "STD-001",
                IsVip = false,
                ImportedPhoneSource = ImportedPhoneSource.Phone1,
                RawPhoneNumber = "6502530101",
                ImportBatchId = importBatch.Id,
                PhoneNumberRecordId = standardRecord.Id
            });
        await database.DbContext.SaveChangesAsync();

        var lookupClient = new FakeTwilioLookupClient(
            SuccessfulLookupResults(vipRecord.CanonicalPhoneNumber));
        var workflow = CreateWorkflow(database.DbContext, lookupClient);

        var result = await workflow.RunAsync(CustomerSegment.Vip, fullRefresh: true);

        Assert.Equal(CustomerSegment.Vip, result.CustomerSegment);
        Assert.Equal(1, result.SelectedRecords);
        Assert.Equal(1, result.ProcessedRecords);
        Assert.Equal(1, result.UpdatedRecords);
        Assert.Equal(0, result.SkippedRecords);
        Assert.Equal([vipRecord.CanonicalPhoneNumber], lookupClient.RequestedNumbers);
    }

    [Fact]
    public async Task RunAsyncPersistsSuccessfulLookupFactsAndDerivedEligibility()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var phoneNumberRecord = CreatePhoneNumberRecord("+16502530020");
        phoneNumberRecord.EnrichmentFailureStatus = EnrichmentFailureStatus.Permanent;
        phoneNumberRecord.LastEnrichmentErrorCode = "20404";
        phoneNumberRecord.LastEnrichmentErrorMessage = "Old failure";

        await database.DbContext.PhoneNumberRecords.AddAsync(phoneNumberRecord);
        await database.DbContext.SaveChangesAsync();

        var lookupClient = new FakeTwilioLookupClient(new Dictionary<string, TwilioLookupResult>
        {
            [phoneNumberRecord.CanonicalPhoneNumber] = TwilioLookupResult.Success(
                lookupPhoneNumber: phoneNumberRecord.CanonicalPhoneNumber,
                isValid: true,
                validationErrors: Array.Empty<string>(),
                countryCode: "US",
                lineType: "mobile",
                carrierName: "Twilio Wireless",
                mobileCountryCode: "310",
                mobileNetworkCode: "260",
                rawPayloadJson: """{"line_type_intelligence":{"type":"mobile"}}""")
        });
        var workflow = CreateWorkflow(database.DbContext, lookupClient);

        var result = await workflow.RunAsync();
        var storedRecord = await database.DbContext.PhoneNumberRecords.SingleAsync();

        Assert.Equal(1, result.ProcessedRecords);
        Assert.Equal(1, result.UpdatedRecords);
        Assert.True(storedRecord.LastEnrichmentAttemptedAtUtc.HasValue);
        Assert.True(storedRecord.LastEnrichedAtUtc.HasValue);
        Assert.Equal("US", storedRecord.TwilioCountryCode);
        Assert.Equal("mobile", storedRecord.TwilioLineType);
        Assert.Equal("Twilio Wireless", storedRecord.TwilioCarrierName);
        Assert.Equal("310", storedRecord.TwilioMobileCountryCode);
        Assert.Equal("260", storedRecord.TwilioMobileNetworkCode);
        Assert.Equal(CampaignEligibilityStatus.Eligible, storedRecord.CampaignEligibilityStatus);
        Assert.Equal(EnrichmentFailureStatus.None, storedRecord.EnrichmentFailureStatus);
        Assert.Null(storedRecord.LastEnrichmentFailedAtUtc);
        Assert.Null(storedRecord.LastEnrichmentErrorCode);
        Assert.Null(storedRecord.LastEnrichmentErrorMessage);
        Assert.NotNull(storedRecord.TwilioLookupPayloadJson);
    }

    [Fact]
    public async Task RunAsyncFailurePreservesPreviousSuccessfulSnapshot()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var previousSuccessUtc = DateTime.UtcNow.AddDays(-7);
        var phoneNumberRecord = CreatePhoneNumberRecord("+16502530030");
        phoneNumberRecord.LastEnrichedAtUtc = previousSuccessUtc;
        phoneNumberRecord.TwilioCountryCode = "US";
        phoneNumberRecord.TwilioLineType = "mobile";
        phoneNumberRecord.TwilioCarrierName = "Existing Carrier";
        phoneNumberRecord.TwilioMobileCountryCode = "310";
        phoneNumberRecord.TwilioMobileNetworkCode = "410";
        phoneNumberRecord.TwilioLookupPayloadJson = """{"previous":"snapshot"}""";
        phoneNumberRecord.CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible;

        await database.DbContext.PhoneNumberRecords.AddAsync(phoneNumberRecord);
        await database.DbContext.SaveChangesAsync();

        var lookupClient = new FakeTwilioLookupClient(new Dictionary<string, TwilioLookupResult>
        {
            [phoneNumberRecord.CanonicalPhoneNumber] = TwilioLookupResult.Failure(
                EnrichmentFailureStatus.Retryable,
                "lookup_transport_failure",
                "Temporary provider outage.")
        });
        var workflow = CreateWorkflow(database.DbContext, lookupClient);

        var result = await workflow.RunAsync(fullRefresh: true);
        var storedRecord = await database.DbContext.PhoneNumberRecords.SingleAsync();

        Assert.Equal(1, result.ProcessedRecords);
        Assert.Equal(0, result.UpdatedRecords);
        Assert.Equal(1, result.FailedRecords);
        Assert.Equal(previousSuccessUtc, storedRecord.LastEnrichedAtUtc);
        Assert.Equal("US", storedRecord.TwilioCountryCode);
        Assert.Equal("mobile", storedRecord.TwilioLineType);
        Assert.Equal("Existing Carrier", storedRecord.TwilioCarrierName);
        Assert.Equal("310", storedRecord.TwilioMobileCountryCode);
        Assert.Equal("410", storedRecord.TwilioMobileNetworkCode);
        Assert.Equal("""{"previous":"snapshot"}""", storedRecord.TwilioLookupPayloadJson);
        Assert.Equal(EnrichmentFailureStatus.Retryable, storedRecord.EnrichmentFailureStatus);
        Assert.Equal(CampaignEligibilityStatus.Ineligible, storedRecord.CampaignEligibilityStatus);
        Assert.True(storedRecord.LastEnrichmentAttemptedAtUtc.HasValue);
        Assert.True(storedRecord.LastEnrichmentFailedAtUtc.HasValue);
        Assert.Equal("lookup_transport_failure", storedRecord.LastEnrichmentErrorCode);
        Assert.Equal("Temporary provider outage.", storedRecord.LastEnrichmentErrorMessage);
    }

    private static EnrichmentWorkflow CreateWorkflow(
        PenelopeSmsDbContext dbContext,
        ITwilioLookupClient twilioLookupClient)
    {
        return new EnrichmentWorkflow(
            new EnrichmentTargetingQuery(dbContext),
            new PhoneNumberEnrichmentRepository(dbContext),
            twilioLookupClient);
    }

    private static PhoneNumberRecord CreatePhoneNumberRecord(string canonicalPhoneNumber)
    {
        return new PhoneNumberRecord
        {
            CanonicalPhoneNumber = canonicalPhoneNumber,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-60),
            LastImportedAtUtc = DateTime.UtcNow.AddDays(-1)
        };
    }

    private static Dictionary<string, TwilioLookupResult> SuccessfulLookupResults(params string[] phoneNumbers)
    {
        return phoneNumbers.ToDictionary(
            phoneNumber => phoneNumber,
            phoneNumber => TwilioLookupResult.Success(
                lookupPhoneNumber: phoneNumber,
                isValid: true,
                validationErrors: Array.Empty<string>(),
                countryCode: "US",
                lineType: "mobile",
                carrierName: "Twilio Wireless",
                mobileCountryCode: "310",
                mobileNetworkCode: "260",
                rawPayloadJson: """{"line_type_intelligence":{"type":"mobile"}}"""));
    }

    private sealed class FakeTwilioLookupClient(Dictionary<string, TwilioLookupResult> results) : ITwilioLookupClient
    {
        public List<string> RequestedNumbers { get; } = [];

        public Task<TwilioLookupResult> LookupAsync(
            string phoneNumber,
            CancellationToken cancellationToken = default)
        {
            RequestedNumbers.Add(phoneNumber);
            return Task.FromResult(results[phoneNumber]);
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
