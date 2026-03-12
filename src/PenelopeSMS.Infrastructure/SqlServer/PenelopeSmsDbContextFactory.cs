using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PenelopeSMS.Infrastructure.SqlServer;

public sealed class PenelopeSmsDbContextFactory : IDesignTimeDbContextFactory<PenelopeSmsDbContext>
{
    public PenelopeSmsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PenelopeSmsDbContext>();

        optionsBuilder.UseSqlServer(
            @"Server=.\SQLEXPRESS;Database=PenelopeSMS;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True");

        return new PenelopeSmsDbContext(optionsBuilder.Options);
    }
}
