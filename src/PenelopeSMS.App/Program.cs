using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PenelopeSMS.App.Menu;
using PenelopeSMS.App.Options;
using PenelopeSMS.App.Templates;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Infrastructure;

namespace PenelopeSMS.App;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var host = BuildHost(args);
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MainMenu>().RunAsync();
        return 0;
    }

    public static IHost BuildHost(
        string[]? args = null,
        Action<HostApplicationBuilder>? configureBuilder = null)
    {
        var builder = Host.CreateApplicationBuilder(args ?? []);
        configureBuilder?.Invoke(builder);

        ConfigureServices(builder);

        return builder.Build();
    }

    internal static void ConfigureServices(HostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<OracleOptions>()
            .BindConfiguration(OracleOptions.SectionName);

        builder.Services
            .AddOptions<SqlServerOptions>()
            .BindConfiguration(SqlServerOptions.SectionName);

        builder.Services
            .AddOptions<TwilioOptions>()
            .BindConfiguration(TwilioOptions.SectionName);

        builder.Services
            .AddOptions<AwsOptions>()
            .BindConfiguration(AwsOptions.SectionName);

        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddScoped<IPlainTextTemplateLoader, FilePlainTextTemplateLoader>();
        builder.Services.AddScoped<ICampaignCreationWorkflow, CampaignCreationWorkflow>();
        builder.Services.AddScoped<ICampaignSendWorkflow, CampaignSendWorkflow>();
        builder.Services.AddScoped<IEnrichmentWorkflow, EnrichmentWorkflow>();
        builder.Services.AddScoped<IEnrichmentRetryWorkflow, EnrichmentRetryWorkflow>();
        builder.Services.AddScoped<IImportWorkflow, ImportWorkflow>();
        builder.Services.AddScoped<CampaignMenuAction>();
        builder.Services.AddScoped<EnrichmentFailureMenuAction>();
        builder.Services.AddScoped<EnrichmentMenuAction>();
        builder.Services.AddScoped<ImportMenuAction>();
        builder.Services.AddScoped<MainMenu>();
    }
}
