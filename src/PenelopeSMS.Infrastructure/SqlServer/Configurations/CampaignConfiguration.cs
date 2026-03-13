using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenelopeSMS.Domain.Entities;

namespace PenelopeSMS.Infrastructure.SqlServer.Configurations;

public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("Campaigns");

        builder.HasKey(campaign => campaign.Id);

        builder.Property(campaign => campaign.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(campaign => campaign.TemplateFilePath)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(campaign => campaign.TemplateBody)
            .IsRequired();

        builder.Property(campaign => campaign.BatchSize)
            .IsRequired();

        builder.Property(campaign => campaign.AudienceSegment)
            .IsRequired();

        builder.HasMany(campaign => campaign.Recipients)
            .WithOne(recipient => recipient.Campaign)
            .HasForeignKey(recipient => recipient.CampaignId);
    }
}
