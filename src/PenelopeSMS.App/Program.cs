using System.Diagnostics;
using System.Runtime.InteropServices;
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
        EnsureDebugConsole();

        using var host = BuildHost(args);
        await host.StartAsync();

        try
        {
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
        builder.Services.AddSingleton<IOperationsMonitor, OperationsMonitor>();
        builder.Services.AddScoped<IPlainTextTemplateLoader, FilePlainTextTemplateLoader>();
        builder.Services.AddScoped<ICampaignCreationWorkflow, CampaignCreationWorkflow>();
        builder.Services.AddScoped<ICampaignSendWorkflow, CampaignSendWorkflow>();
        builder.Services.AddScoped<IDeliveryCallbackProcessingWorkflow, DeliveryCallbackProcessingWorkflow>();
        builder.Services.AddHostedService<DeliveryCallbackWorker>();
        builder.Services.AddScoped<IEnrichmentWorkflow, EnrichmentWorkflow>();
        builder.Services.AddScoped<IEnrichmentRetryWorkflow, EnrichmentRetryWorkflow>();
        builder.Services.AddScoped<IImportWorkflow, ImportWorkflow>();
        builder.Services.AddScoped<IMonitoringWorkflow, MonitoringWorkflow>();
        builder.Services.AddScoped<CampaignMenuAction>();
        builder.Services.AddScoped<EnrichmentFailureMenuAction>();
        builder.Services.AddScoped<EnrichmentMenuAction>();
        builder.Services.AddScoped<ImportMenuAction>();
        builder.Services.AddScoped<MonitoringMenuAction>();
        builder.Services.AddScoped<MonitoringScreenRenderer>();
        builder.Services.AddScoped<MainMenu>();
    }

    private static void EnsureDebugConsole()
    {
        if (!Debugger.IsAttached || !OperatingSystem.IsWindows() || GetConsoleWindow() != IntPtr.Zero)
        {
            return;
        }

        if (!AllocConsole())
        {
            return;
        }

        Console.SetIn(new StreamReader(Console.OpenStandardInput()));
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        Console.Title = "PenelopeSMS Debug";
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetConsoleWindow();
}
