using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PenelopeSMS.Infrastructure.SqlServer;

public sealed class PenelopeSmsDbContextFactory : IDesignTimeDbContextFactory<PenelopeSmsDbContext>
{
    public PenelopeSmsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PenelopeSmsDbContext>();

        optionsBuilder.UseSqlServer(
            @"Server=tcp:127.0.0.1,1433;Database=PenelopeSMS;User Id=sqlpublish;Password=Rivers1957&&;Encrypt=False;TrustServerCertificate=True");

        return new PenelopeSmsDbContext(optionsBuilder.Options);
    }
}
