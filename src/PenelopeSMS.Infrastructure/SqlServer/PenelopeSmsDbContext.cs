using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Entities;

namespace PenelopeSMS.Infrastructure.SqlServer;

public sealed class PenelopeSmsDbContext(DbContextOptions<PenelopeSmsDbContext> options) : DbContext(options)
{
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<PhoneNumberRecord> PhoneNumberRecords => Set<PhoneNumberRecord>();

    public DbSet<CustomerPhoneLink> CustomerPhoneLinks => Set<CustomerPhoneLink>();

    public DbSet<Campaign> Campaigns => Set<Campaign>();

    public DbSet<CampaignRecipient> CampaignRecipients => Set<CampaignRecipient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PenelopeSmsDbContext).Assembly);
    }
}
