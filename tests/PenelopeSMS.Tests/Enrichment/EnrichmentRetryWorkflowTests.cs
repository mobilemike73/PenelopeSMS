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

public sealed class EnrichmentRetryWorkflowTests
{
    [Fact]
    public async Task ReviewFailuresAsyncProjectsRetryabilityAndEligibility()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var retryable = CreateFailedRecord(
            "+16502530100",
            EnrichmentFailureStatus.Retryable,
            "lookup_transport_failure",
            "Temporary outage.");
        var permanent = CreateFailedRecord(
            "+16502530101",
            EnrichmentFailureStatus.Permanent,
            "21614",
            "Invalid number.");
        var success = new PhoneNumberRecord
        {
            CanonicalPhoneNumber = "+16502530102",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-60),
            LastImportedAtUtc = DateTime.UtcNow.AddDays(-1),
            LastEnrichedAtUtc = DateTime.UtcNow.AddDays(-1),
            CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible
        };

        await database.DbContext.PhoneNumberRecords.AddRangeAsync(retryable, permanent, success);
        await database.DbContext.SaveChangesAsync();

        var workflow = CreateWorkflow(database.DbContext, new FakeTwilioLookupClient([]));

        var failures = await workflow.ReviewFailuresAsync();

        Assert.Equal(2, failures.Count);

        var retryableFailure = Assert.Single(failures, failure => failure.PhoneNumberRecordId == retryable.Id);
        Assert.True(retryableFailure.IsRetryable);
        Assert.Equal(CampaignEligibilityStatus.Ineligible, retryableFailure.EligibilityStatus);
        Assert.Equal("lookup_transport_failure", retryableFailure.ErrorCode);

        var permanentFailure = Assert.Single(failures, failure => failure.PhoneNumberRecordId == permanent.Id);
        Assert.False(permanentFailure.IsRetryable);
        Assert.Equal(CampaignEligibilityStatus.Ineligible, permanentFailure.EligibilityStatus);
        Assert.Equal("21614", permanentFailure.ErrorCode);
    }

    [Fact]
    public async Task RetryAllAsyncRetriesOnlyRetryableFailures()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var retryable = CreateFailedRecord(
            "+16502530110",
            EnrichmentFailureStatus.Retryable,
            "lookup_transport_failure",
            "Temporary outage.");
        var permanent = CreateFailedRecord(
            "+16502530111",
            EnrichmentFailureStatus.Permanent,
            "21614",
            "Invalid number.");

        await database.DbContext.PhoneNumberRecords.AddRangeAsync(retryable, permanent);
        await database.DbContext.SaveChangesAsync();

        var lookupClient = new FakeTwilioLookupClient(new Dictionary<string, TwilioLookupResult>
        {
            [retryable.CanonicalPhoneNumber] = SuccessfulLookup(retryable.CanonicalPhoneNumber)
        });
        var workflow = CreateWorkflow(database.DbContext, lookupClient);

        var result = await workflow.RetryAllAsync();
        var storedRetryable = await database.DbContext.PhoneNumberRecords.SingleAsync(record => record.Id == retryable.Id);
        var storedPermanent = await database.DbContext.PhoneNumberRecords.SingleAsync(record => record.Id == permanent.Id);

        Assert.Equal(2, result.RequestedRecords);
        Assert.Equal(1, result.ProcessedRecords);
        Assert.Equal(1, result.UpdatedRecords);
        Assert.Equal(0, result.FailedRecords);
        Assert.Equal(1, result.SkippedRecords);
        Assert.Equal([retryable.CanonicalPhoneNumber], lookupClient.RequestedNumbers);
        Assert.Equal(EnrichmentFailureStatus.None, storedRetryable.EnrichmentFailureStatus);
        Assert.Equal(CampaignEligibilityStatus.Eligible, storedRetryable.CampaignEligibilityStatus);
        Assert.Equal(EnrichmentFailureStatus.Permanent, storedPermanent.EnrichmentFailureStatus);
        Assert.Equal(CampaignEligibilityStatus.Ineligible, storedPermanent.CampaignEligibilityStatus);
    }

    [Fact]
    public async Task RetrySelectedAsyncRespectsExplicitRecordChoicesAndSkipsNonRetryableSelections()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var firstRetryable = CreateFailedRecord(
            "+16502530120",
            EnrichmentFailureStatus.Retryable,
            "lookup_transport_failure",
            "Temporary outage.");
        var secondRetryable = CreateFailedRecord(
            "+16502530121",
            EnrichmentFailureStatus.Retryable,
            "lookup_transport_failure",
            "Temporary outage.");
        var permanent = CreateFailedRecord(
            "+16502530122",
            EnrichmentFailureStatus.Permanent,
            "21614",
            "Invalid number.");

        await database.DbContext.PhoneNumberRecords.AddRangeAsync(firstRetryable, secondRetryable, permanent);
        await database.DbContext.SaveChangesAsync();

        var lookupClient = new FakeTwilioLookupClient(new Dictionary<string, TwilioLookupResult>
        {
            [secondRetryable.CanonicalPhoneNumber] = SuccessfulLookup(secondRetryable.CanonicalPhoneNumber)
        });
        var workflow = CreateWorkflow(database.DbContext, lookupClient);

        var result = await workflow.RetrySelectedAsync([secondRetryable.Id, permanent.Id]);
        var storedFirstRetryable = await database.DbContext.PhoneNumberRecords.SingleAsync(record => record.Id == firstRetryable.Id);
        var storedSecondRetryable = await database.DbContext.PhoneNumberRecords.SingleAsync(record => record.Id == secondRetryable.Id);
        var storedPermanent = await database.DbContext.PhoneNumberRecords.SingleAsync(record => record.Id == permanent.Id);

        Assert.Equal(2, result.RequestedRecords);
        Assert.Equal(1, result.ProcessedRecords);
        Assert.Equal(1, result.UpdatedRecords);
        Assert.Equal(0, result.FailedRecords);
        Assert.Equal(1, result.SkippedRecords);
        Assert.Equal([secondRetryable.CanonicalPhoneNumber], lookupClient.RequestedNumbers);
        Assert.Equal(EnrichmentFailureStatus.Retryable, storedFirstRetryable.EnrichmentFailureStatus);
        Assert.Equal(EnrichmentFailureStatus.None, storedSecondRetryable.EnrichmentFailureStatus);
        Assert.Equal(EnrichmentFailureStatus.Permanent, storedPermanent.EnrichmentFailureStatus);
    }

    private static EnrichmentRetryWorkflow CreateWorkflow(
        PenelopeSmsDbContext dbContext,
        ITwilioLookupClient twilioLookupClient)
    {
        return new EnrichmentRetryWorkflow(
            new FailedEnrichmentReviewQuery(dbContext),
            new PhoneNumberEnrichmentRepository(dbContext),
            twilioLookupClient);
    }

    private static PhoneNumberRecord CreateFailedRecord(
        string canonicalPhoneNumber,
        EnrichmentFailureStatus failureStatus,
        string errorCode,
        string errorMessage)
    {
        return new PhoneNumberRecord
        {
            CanonicalPhoneNumber = canonicalPhoneNumber,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-60),
            LastImportedAtUtc = DateTime.UtcNow.AddDays(-1),
            LastEnrichmentAttemptedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            LastEnrichmentFailedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            CampaignEligibilityStatus = CampaignEligibilityStatus.Ineligible,
            EligibilityEvaluatedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            EnrichmentFailureStatus = failureStatus,
            LastEnrichmentErrorCode = errorCode,
            LastEnrichmentErrorMessage = errorMessage
        };
    }

    private static TwilioLookupResult SuccessfulLookup(string phoneNumber)
    {
        return TwilioLookupResult.Success(
            lookupPhoneNumber: phoneNumber,
            isValid: true,
            validationErrors: Array.Empty<string>(),
            countryCode: "US",
            lineType: "mobile",
            carrierName: "Twilio Wireless",
            mobileCountryCode: "310",
            mobileNetworkCode: "260",
            rawPayloadJson: """{"line_type_intelligence":{"type":"mobile"}}""");
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
