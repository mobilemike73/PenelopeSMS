using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PenelopeSMS.App;
using PenelopeSMS.App.Menu;
using PenelopeSMS.App.Options;
using PenelopeSMS.App.Rendering;
using PenelopeSMS.App.Services;
using PenelopeSMS.App.Workflows;

namespace PenelopeSMS.Tests.Host;

public sealed class HostBootstrapTests
{
    [Fact]
    public void BuildHostResolvesMainMenuWithoutExternalCredentials()
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var menu = scope.ServiceProvider.GetRequiredService<MainMenu>();

        Assert.NotNull(menu);
    }

    [Fact]
    public void BuildHostResolvesMonitoringMenuAndWorkflow()
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();

        var monitoringMenu = scope.ServiceProvider.GetRequiredService<MonitoringMenuAction>();
        var monitoringWorkflow = scope.ServiceProvider.GetRequiredService<IMonitoringWorkflow>();
        var renderer = scope.ServiceProvider.GetRequiredService<MonitoringScreenRenderer>();

        Assert.NotNull(monitoringMenu);
        Assert.NotNull(monitoringWorkflow);
        Assert.NotNull(renderer);
    }

    [Fact]
    public void BuildHostBindsExpectedConfigurationKeys()
    {
        var configuration = new Dictionary<string, string?>
        {
            [ConfigurationPath.Combine(OracleOptions.SectionName, nameof(OracleOptions.ConnectionString))] = "Data Source=oracle-host;",
            [ConfigurationPath.Combine(OracleOptions.SectionName, nameof(OracleOptions.ImportQuery))] = "select 1 from dual",
            [ConfigurationPath.Combine(OracleOptions.SectionName, nameof(OracleOptions.DefaultRegion))] = "CA",
            [ConfigurationPath.Combine(OracleOptions.SectionName, nameof(OracleOptions.CommandTimeoutSeconds))] = "45",
            [ConfigurationPath.Combine(SqlServerOptions.SectionName, nameof(SqlServerOptions.ConnectionString))] = "Server=.;Database=PenelopeSMS;",
            [ConfigurationPath.Combine(SqlServerOptions.SectionName, nameof(SqlServerOptions.CommandTimeoutSeconds))] = "30",
            [ConfigurationPath.Combine(TwilioOptions.SectionName, nameof(TwilioOptions.AccountSid))] = "AC123",
            [ConfigurationPath.Combine(TwilioOptions.SectionName, nameof(TwilioOptions.AuthToken))] = "secret",
            [ConfigurationPath.Combine(TwilioOptions.SectionName, nameof(TwilioOptions.MessagingServiceSid))] = "MG123",
            [ConfigurationPath.Combine(TwilioOptions.SectionName, nameof(TwilioOptions.StatusCallbackUrl))] = "https://callbacks.example.com/twilio/status-callback",
            [ConfigurationPath.Combine(AwsOptions.SectionName, nameof(AwsOptions.Region))] = "us-west-2",
            [ConfigurationPath.Combine(AwsOptions.SectionName, nameof(AwsOptions.AccessKeyId))] = "key",
            [ConfigurationPath.Combine(AwsOptions.SectionName, nameof(AwsOptions.SecretAccessKey))] = "secret",
            [ConfigurationPath.Combine(AwsOptions.SectionName, nameof(AwsOptions.CallbackQueueUrl))] = "https://sqs.example.com/queue",
            [ConfigurationPath.Combine(AwsOptions.SectionName, nameof(AwsOptions.CallbackDeadLetterQueueUrl))] = "https://sqs.example.com/queue-dlq"
        };

        using var host = CreateHost(overrides: configuration);

        var oracle = host.Services.GetRequiredService<IOptions<OracleOptions>>().Value;
        var sqlServer = host.Services.GetRequiredService<IOptions<SqlServerOptions>>().Value;
        var twilio = host.Services.GetRequiredService<IOptions<TwilioOptions>>().Value;
        var aws = host.Services.GetRequiredService<IOptions<AwsOptions>>().Value;

        Assert.Equal("Data Source=oracle-host;", oracle.ConnectionString);
        Assert.Equal("select 1 from dual", oracle.ImportQuery);
        Assert.Equal("CA", oracle.DefaultRegion);
        Assert.Equal(45, oracle.CommandTimeoutSeconds);

        Assert.Equal("Server=.;Database=PenelopeSMS;", sqlServer.ConnectionString);
        Assert.Equal(30, sqlServer.CommandTimeoutSeconds);

        Assert.Equal("AC123", twilio.AccountSid);
        Assert.Equal("secret", twilio.AuthToken);
        Assert.Equal("MG123", twilio.MessagingServiceSid);
        Assert.Equal("https://callbacks.example.com/twilio/status-callback", twilio.StatusCallbackUrl);

        Assert.Equal("us-west-2", aws.Region);
        Assert.Equal("key", aws.AccessKeyId);
        Assert.Equal("secret", aws.SecretAccessKey);
        Assert.Equal("https://sqs.example.com/queue", aws.CallbackQueueUrl);
        Assert.Equal("https://sqs.example.com/queue-dlq", aws.CallbackDeadLetterQueueUrl);
    }

    [Fact]
    public void BuildHostRegistersDeliveryCallbackWorkerAsHostedService()
    {
        using var host = CreateHost(AppMode.Worker);

        var hostedServices = host.Services.GetServices<IHostedService>();

        Assert.Contains(hostedServices, service => service is DeliveryCallbackWorker);
    }

    [Fact]
    public void BuildHostDoesNotRegisterDeliveryCallbackWorkerInUiMode()
    {
        using var host = CreateHost(AppMode.Ui);

        var hostedServices = host.Services.GetServices<IHostedService>();

        Assert.DoesNotContain(hostedServices, service => service is DeliveryCallbackWorker);
        Assert.Contains(hostedServices, service => service is CampaignSendDispatcher);
    }

    [Fact]
    public void BuildHostDoesNotRegisterCampaignSendDispatcherInWorkerMode()
    {
        using var host = CreateHost(AppMode.Worker);

        var hostedServices = host.Services.GetServices<IHostedService>();

        Assert.DoesNotContain(hostedServices, service => service is CampaignSendDispatcher);
    }

    [Fact]
    public void ResolveAppModeDefaultsToUiMode()
    {
        var result = Program.ResolveAppMode(["--foo", "bar"]);

        Assert.Equal(AppMode.Ui, result.Mode);
        Assert.Equal(2, result.HostArgs.Length);
    }

    [Fact]
    public void ResolveAppModeParsesUiModePrefix()
    {
        var result = Program.ResolveAppMode(["ui"]);

        Assert.Equal(AppMode.Ui, result.Mode);
        Assert.Empty(result.HostArgs);
    }

    [Fact]
    public void ResolveAppModeParsesWorkerModePrefix()
    {
        var result = Program.ResolveAppMode(["worker", "--foo", "bar"]);

        Assert.Equal(AppMode.Worker, result.Mode);
        Assert.Equal(["--foo", "bar"], result.HostArgs);
    }

    private static IHost CreateHost(
        AppMode mode = AppMode.Ui,
        IDictionary<string, string?>? overrides = null)
    {
        return Program.BuildHost(
            mode,
            configureBuilder: builder =>
            {
                if (overrides is not null)
                {
                    builder.Configuration.AddInMemoryCollection(overrides);
                }
            });
    }
}
