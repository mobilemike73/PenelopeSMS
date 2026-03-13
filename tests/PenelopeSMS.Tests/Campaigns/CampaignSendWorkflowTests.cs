using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Domain.Entities;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.SqlServer;
using PenelopeSMS.Infrastructure.SqlServer.Queries;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;
using PenelopeSMS.Infrastructure.Twilio;

namespace PenelopeSMS.Tests.Campaigns;

public sealed class CampaignSendWorkflowTests
{
    [Fact]
    public async Task SendNextBatchAsyncHonorsBatchSizeAndPersistsAcceptedResults()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedCampaignAsync(database.DbContext);
        var sentPhoneNumbers = new List<string>();
        var sender = new FakeTwilioMessageSender((phoneNumber, _) =>
        {
            sentPhoneNumbers.Add(phoneNumber);
            return Task.FromResult(TwilioSendResult.Success($"SM-{sentPhoneNumbers.Count}", "accepted"));
        });

        var workflow = CreateWorkflow(database.DbContext, sender);

        var result = await workflow.SendNextBatchAsync(1);

        var recipients = await database.DbContext.CampaignRecipients
            .OrderBy(recipient => recipient.Id)
            .ToListAsync();
        var campaign = await database.DbContext.Campaigns.SingleAsync();

        Assert.Equal(2, result.AttemptedRecipients);
        Assert.Equal(2, result.AcceptedRecipients);
        Assert.Equal(0, result.FailedRecipients);
        Assert.Equal(1, result.RemainingPendingRecipients);
        Assert.Equal(
            ["+16502530000", "+16502530001"],
            sentPhoneNumbers);
        Assert.Equal(CampaignStatus.Sending, campaign.Status);
        Assert.NotNull(campaign.StartedAtUtc);
        Assert.Equal(CampaignRecipientStatus.Submitted, recipients[0].Status);
        Assert.Equal("SM-1", recipients[0].TwilioMessageSid);
        Assert.Equal("accepted", recipients[0].InitialTwilioStatus);
        Assert.Equal(CampaignRecipientStatus.Submitted, recipients[1].Status);
        Assert.Equal(CampaignRecipientStatus.Pending, recipients[2].Status);
    }

    [Fact]
    public async Task SendNextBatchAsyncPersistsImmediateFailuresAndSkipsSubmittedRecipients()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedCampaignAsync(database.DbContext, markFirstRecipientSubmitted: true);
        var attemptedPhoneNumbers = new List<string>();
        var sender = new FakeTwilioMessageSender((phoneNumber, _) =>
        {
            attemptedPhoneNumbers.Add(phoneNumber);

            if (phoneNumber == "+16502530001")
            {
                return Task.FromResult(TwilioSendResult.Failure("21610", "Message cannot be sent."));
            }

            return Task.FromResult(TwilioSendResult.Success("SM-final", "queued"));
        });

        var workflow = CreateWorkflow(database.DbContext, sender);

        var result = await workflow.SendNextBatchAsync(1);

        var recipients = await database.DbContext.CampaignRecipients
            .OrderBy(recipient => recipient.Id)
            .ToListAsync();
        var campaign = await database.DbContext.Campaigns.SingleAsync();

        Assert.Equal(2, result.AttemptedRecipients);
        Assert.Equal(1, result.AcceptedRecipients);
        Assert.Equal(1, result.FailedRecipients);
        Assert.Equal(0, result.RemainingPendingRecipients);
        Assert.Equal(
            ["+16502530001", "+16502530002"],
            attemptedPhoneNumbers);
        Assert.Equal(CampaignRecipientStatus.Submitted, recipients[0].Status);
        Assert.Equal("SM-existing", recipients[0].TwilioMessageSid);
        Assert.Equal(CampaignRecipientStatus.Failed, recipients[1].Status);
        Assert.Equal("21610", recipients[1].ProviderErrorCode);
        Assert.Equal("Message cannot be sent.", recipients[1].ProviderErrorMessage);
        Assert.Equal(CampaignRecipientStatus.Submitted, recipients[2].Status);
        Assert.Equal("SM-final", recipients[2].TwilioMessageSid);
        Assert.Equal(CampaignStatus.Completed, campaign.Status);
        Assert.NotNull(campaign.CompletedAtUtc);
    }

    [Fact]
    public async Task SendNextBatchAsyncWritesVerboseFailureDetails()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedCampaignAsync(database.DbContext, markFirstRecipientSubmitted: true);
        var sender = new FakeTwilioMessageSender((phoneNumber, _) =>
        {
            return Task.FromResult(phoneNumber == "+16502530001"
                ? TwilioSendResult.Failure("21610", "Message cannot be sent.")
                : TwilioSendResult.Success("SM-final", "queued"));
        });
        var output = new StringWriter();
        var workflow = CreateWorkflow(database.DbContext, sender, output);

        await workflow.SendNextBatchAsync(1);

        var consoleText = output.ToString();
        Assert.Contains("Send failed for +16502530001: 21610 | Message cannot be sent.", consoleText);
        Assert.Contains("Sent to +16502530002: accepted as queued", consoleText);
    }

    [Fact]
    public async Task SendNextBatchAsyncUsesParallelRuntimePathWhenScopeFactoryIsAvailable()
    {
        await using var database = await ScopedSqliteTestDatabase.CreateAsync();
        await using var seedScope = database.Services.CreateAsyncScope();
        var seedDbContext = seedScope.ServiceProvider.GetRequiredService<PenelopeSmsDbContext>();
        await SeedCampaignAsync(seedDbContext);

        var sender = database.Sender;
        var output = new StringWriter();

        await using var workflowScope = database.Services.CreateAsyncScope();
        var workflow = new CampaignSendWorkflow(
            workflowScope.ServiceProvider.GetRequiredService<CampaignSendBatchQuery>(),
            workflowScope.ServiceProvider.GetRequiredService<CampaignSendRepository>(),
            workflowScope.ServiceProvider.GetRequiredService<ITwilioMessageSender>(),
            runtimeOutput: output,
            serviceScopeFactory: workflowScope.ServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var result = await workflow.SendNextBatchAsync(1);

        await using var assertScope = database.Services.CreateAsyncScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<PenelopeSmsDbContext>();
        var recipients = await assertDbContext.CampaignRecipients
            .OrderBy(recipient => recipient.Id)
            .ToListAsync();

        Assert.Equal(2, result.AttemptedRecipients);
        Assert.Equal(2, result.AcceptedRecipients);
        Assert.Equal(1, result.RemainingPendingRecipients);
        Assert.True(sender.MaxConcurrentCalls > 1);
        Assert.Equal(
            ["+16502530000", "+16502530001"],
            sender.AttemptedPhoneNumbers.OrderBy(phoneNumber => phoneNumber).ToArray());
        Assert.All(recipients.Take(2), recipient => Assert.Equal(CampaignRecipientStatus.Submitted, recipient.Status));
        Assert.Equal(CampaignRecipientStatus.Pending, recipients[2].Status);
    }

    private static CampaignSendWorkflow CreateWorkflow(
        PenelopeSmsDbContext dbContext,
        ITwilioMessageSender twilioMessageSender,
        TextWriter? output = null)
    {
        return new CampaignSendWorkflow(
            new CampaignSendBatchQuery(dbContext),
            new CampaignSendRepository(dbContext),
            twilioMessageSender,
            runtimeOutput: output);
    }

    private static async Task SeedCampaignAsync(
        PenelopeSmsDbContext dbContext,
        bool markFirstRecipientSubmitted = false)
    {
        var phoneRecords = new[]
        {
            new PhoneNumberRecord
            {
                CanonicalPhoneNumber = "+16502530000",
                CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible
            },
            new PhoneNumberRecord
            {
                CanonicalPhoneNumber = "+16502530001",
                CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible
            },
            new PhoneNumberRecord
            {
                CanonicalPhoneNumber = "+16502530002",
                CampaignEligibilityStatus = CampaignEligibilityStatus.Eligible
            }
        };

        var campaign = new Campaign
        {
            Name = "March Promo",
            TemplateFilePath = @"C:\templates\march.txt",
            TemplateBody = "Hello from PenelopeSMS",
            BatchSize = 2
        };

        dbContext.PhoneNumberRecords.AddRange(phoneRecords);
        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync();

        dbContext.CampaignRecipients.AddRange(
            new CampaignRecipient
            {
                CampaignId = campaign.Id,
                PhoneNumberRecordId = phoneRecords[0].Id,
                Status = markFirstRecipientSubmitted
                    ? CampaignRecipientStatus.Submitted
                    : CampaignRecipientStatus.Pending,
                TwilioMessageSid = markFirstRecipientSubmitted ? "SM-existing" : null,
                InitialTwilioStatus = markFirstRecipientSubmitted ? "accepted" : null,
                SubmittedAtUtc = markFirstRecipientSubmitted ? DateTime.UtcNow : null
            },
            new CampaignRecipient
            {
                CampaignId = campaign.Id,
                PhoneNumberRecordId = phoneRecords[1].Id
            },
            new CampaignRecipient
            {
                CampaignId = campaign.Id,
                PhoneNumberRecordId = phoneRecords[2].Id
            });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeTwilioMessageSender(
        Func<string, string, Task<TwilioSendResult>> sendAsync) : ITwilioMessageSender
    {
        public Task<TwilioSendResult> SendAsync(
            string toPhoneNumber,
            string messageBody,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return sendAsync(toPhoneNumber, messageBody);
        }
    }

    private sealed class TrackingTwilioMessageSender : ITwilioMessageSender
    {
        private int currentCalls;
        private int maxConcurrentCalls;
        private int sentCount;

        public ConcurrentQueue<string> AttemptedPhoneNumbers { get; } = [];

        public int MaxConcurrentCalls => Volatile.Read(ref maxConcurrentCalls);

        public async Task<TwilioSendResult> SendAsync(
            string toPhoneNumber,
            string messageBody,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AttemptedPhoneNumbers.Enqueue(toPhoneNumber);

            var current = Interlocked.Increment(ref currentCalls);
            UpdateMaxConcurrency(current);

            try
            {
                await Task.Delay(50, cancellationToken);
                var sequence = Interlocked.Increment(ref sentCount);
                return TwilioSendResult.Success($"SM-{sequence}", "accepted");
            }
            finally
            {
                Interlocked.Decrement(ref currentCalls);
            }
        }

        private void UpdateMaxConcurrency(int current)
        {
            while (true)
            {
                var observed = Volatile.Read(ref maxConcurrentCalls);

                if (current <= observed)
                {
                    return;
                }

                if (Interlocked.CompareExchange(ref maxConcurrentCalls, current, observed) == observed)
                {
                    return;
                }
            }
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

    private sealed class ScopedSqliteTestDatabase : IAsyncDisposable
    {
        private ScopedSqliteTestDatabase(
            string databasePath,
            ServiceProvider services,
            TrackingTwilioMessageSender sender)
        {
            DatabasePath = databasePath;
            Services = services;
            Sender = sender;
        }

        public string DatabasePath { get; }

        public ServiceProvider Services { get; }

        public TrackingTwilioMessageSender Sender { get; }

        public static async Task<ScopedSqliteTestDatabase> CreateAsync()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
            var sender = new TrackingTwilioMessageSender();
            var services = new ServiceCollection();

            services.AddDbContext<PenelopeSmsDbContext>(options =>
                options.UseSqlite($"Data Source={databasePath}"));
            services.AddScoped<CampaignSendBatchQuery>();
            services.AddScoped<CampaignSendRepository>();
            services.AddSingleton<ITwilioMessageSender>(sender);

            var serviceProvider = services.BuildServiceProvider();

            await using var scope = serviceProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PenelopeSmsDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            return new ScopedSqliteTestDatabase(databasePath, serviceProvider, sender);
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();

            if (File.Exists(DatabasePath))
            {
                File.Delete(DatabasePath);
            }
        }
    }
}
