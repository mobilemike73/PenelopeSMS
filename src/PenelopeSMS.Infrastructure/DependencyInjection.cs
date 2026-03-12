using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PenelopeSMS.Domain.Services;
using PenelopeSMS.Infrastructure.Oracle;
using PenelopeSMS.Infrastructure.SqlServer;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;

namespace PenelopeSMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration["SqlServer:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString =
                @"Server=.\SQLEXPRESS;Database=PenelopeSMS;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True";
        }

        services.AddDbContext<PenelopeSmsDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        services.AddScoped<ImportPersistenceService>();
        services.AddScoped<IOraclePhoneImportReader, OraclePhoneImportReader>();
        services.AddSingleton<IPhoneNumberNormalizer, PhoneNumberNormalizer>();

        return services;
    }
}
