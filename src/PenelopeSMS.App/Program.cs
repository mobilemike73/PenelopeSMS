using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PenelopeSMS.App.Menu;
using PenelopeSMS.App.Monitoring;
using PenelopeSMS.App.Options;
using PenelopeSMS.App.Rendering;
using PenelopeSMS.App.Services;
using PenelopeSMS.App.Templates;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Infrastructure;

namespace PenelopeSMS.App;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var (mode, hostArgs) = ResolveAppMode(args);
        using var host = BuildHost(mode, hostArgs);
        WriteConfigurationDiagnostics(host.Services);
        await host.StartAsync();

        try
        {
            if (mode == AppMode.Worker)
            {
                await host.WaitForShutdownAsync();
                return 0;
            }

            using var scope = host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MainMenu>().RunAsync();
            return 0;
        }
        finally
        {
            await host.StopAsync();
        }
    }

    public static IHost BuildHost(
        string[]? args = null,
        Action<HostApplicationBuilder>? configureBuilder = null)
    {
        return BuildHost(AppMode.Ui, args, configureBuilder);
    }

    internal static IHost BuildHost(
        AppMode mode,
        string[]? args = null,
        Action<HostApplicationBuilder>? configureBuilder = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args ?? [],
            ContentRootPath = AppContext.BaseDirectory
        });
        configureBuilder?.Invoke(builder);

        ConfigureServices(builder, mode);

        return builder.Build();
    }

    internal static void ConfigureServices(
        HostApplicationBuilder builder,
        AppMode mode = AppMode.Ui)
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
        builder.Services.AddSingleton<IOperationsMonitor, OperationsMonitor>();
        builder.Services.AddSingleton<CampaignSendDispatcher>();
        builder.Services.AddSingleton<ICampaignSendDispatcher>(services => services.GetRequiredService<CampaignSendDispatcher>());
        builder.Services.AddScoped<IPlainTextTemplateLoader, FilePlainTextTemplateLoader>();
        builder.Services.AddScoped<ICampaignCreationWorkflow, CampaignCreationWorkflow>();
        builder.Services.AddScoped<ICampaignSendWorkflow, CampaignSendWorkflow>();
        builder.Services.AddScoped<IDeliveryCallbackProcessingWorkflow, DeliveryCallbackProcessingWorkflow>();
        builder.Services.AddScoped<IEnrichmentWorkflow, EnrichmentWorkflow>();
        builder.Services.AddScoped<IEnrichmentRetryWorkflow, EnrichmentRetryWorkflow>();
        builder.Services.AddScoped<IImportWorkflow, ImportWorkflow>();
        builder.Services.AddScoped<IMonitoringWorkflow, MonitoringWorkflow>();
        builder.Services.AddScoped<CampaignMenuAction>();
        builder.Services.AddScoped<EnrichmentFailureMenuAction>();
        builder.Services.AddScoped<EnrichmentMenuAction>();
        builder.Services.AddScoped<ImportMenuAction>();
        builder.Services.AddScoped<MonitoringMenuAction>();
        builder.Services.AddScoped<MonitoringHtmlReportRenderer>();
        builder.Services.AddScoped<MonitoringScreenRenderer>();
        builder.Services.AddScoped<MainMenu>();

        if (mode == AppMode.Worker)
        {
            builder.Services.AddHostedService<DeliveryCallbackWorker>();
        }
        else
        {
            builder.Services.AddHostedService<CampaignSendDispatcher>(services => services.GetRequiredService<CampaignSendDispatcher>());
        }
    }

    private static void WriteConfigurationDiagnostics(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        Console.WriteLine($"Configuration content root: {environment.ContentRootPath}");
        WriteConfigFile(environment.ContentRootPath, "appsettings.json");
        WriteConfigFile(environment.ContentRootPath, $"appsettings.{environment.EnvironmentName}.json");
        Console.WriteLine();
    }

    private static void WriteConfigFile(string contentRootPath, string fileName)
    {
        var filePath = Path.Combine(contentRootPath, fileName);
        Console.WriteLine($"[{filePath}]");

        if (!File.Exists(filePath))
        {
            Console.WriteLine("File not found.");
            return;
        }

        Console.WriteLine(File.ReadAllText(filePath));
    }

    internal static (AppMode Mode, string[] HostArgs) ResolveAppMode(string[]? args)
    {
        if (args is null || args.Length == 0)
        {
            return (AppMode.Ui, []);
        }

        var firstArg = args[0].Trim();

        if (string.Equals(firstArg, "worker", StringComparison.OrdinalIgnoreCase))
        {
            return (AppMode.Worker, args[1..]);
        }

        if (string.Equals(firstArg, "ui", StringComparison.OrdinalIgnoreCase))
        {
            return (AppMode.Ui, args[1..]);
        }

        return (AppMode.Ui, args);
    }
}

internal enum AppMode
{
    Ui = 0,
    Worker = 1
}
