using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PenelopeSMS.App;
using PenelopeSMS.App.Menu;
using PenelopeSMS.App.Options;

namespace PenelopeSMS.Tests.Host;

public sealed class HostBootstrapTests
{
    [Fact]
    public void BuildHostResolvesMainMenuWithoutExternalCredentials()
    {
        using var host = CreateHost();

        var menu = host.Services.GetRequiredService<MainMenu>();

        Assert.NotNull(menu);
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
            [ConfigurationPath.Combine(AwsOptions.SectionName, nameof(AwsOptions.Region))] = "us-west-2",
            [ConfigurationPath.Combine(AwsOptions.SectionName, nameof(AwsOptions.AccessKeyId))] = "key",
            [ConfigurationPath.Combine(AwsOptions.SectionName, nameof(AwsOptions.SecretAccessKey))] = "secret",
            [ConfigurationPath.Combine(AwsOptions.SectionName, nameof(AwsOptions.CallbackQueueUrl))] = "https://sqs.example.com/queue"
        };

        using var host = CreateHost(configuration);

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

        Assert.Equal("us-west-2", aws.Region);
        Assert.Equal("key", aws.AccessKeyId);
        Assert.Equal("secret", aws.SecretAccessKey);
        Assert.Equal("https://sqs.example.com/queue", aws.CallbackQueueUrl);
    }

    private static IHost CreateHost(IDictionary<string, string?>? overrides = null)
    {
        return Program.BuildHost(
            configureBuilder: builder =>
            {
                if (overrides is not null)
                {
                    builder.Configuration.AddInMemoryCollection(overrides);
                }
            });
    }
}
