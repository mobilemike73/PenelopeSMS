using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenelopeSMS.Domain.Entities;

namespace PenelopeSMS.Infrastructure.SqlServer.Configurations;

public sealed class CampaignRecipientStatusHistoryConfiguration : IEntityTypeConfiguration<CampaignRecipientStatusHistory>
{
    public void Configure(EntityTypeBuilder<CampaignRecipientStatusHistory> builder)
    {
        builder.ToTable("CampaignRecipientStatusHistory");

        builder.HasKey(history => history.Id);

        builder.Property(history => history.ProviderEventRawValue)
            .HasMaxLength(128);

        builder.Property(history => history.ProviderErrorCode)
            .HasMaxLength(64);

        builder.Property(history => history.ProviderErrorMessage)
            .HasMaxLength(1024);

        builder.Property(history => history.RawPayloadJson)
            .IsRequired();

        builder.Property(history => history.CallbackFingerprint)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(history => new
            {
                history.CampaignRecipientId,
                history.CallbackFingerprint
            })
            .IsUnique();

        builder.HasOne(history => history.CampaignRecipient)
            .WithMany(recipient => recipient.StatusHistory)
            .HasForeignKey(history => history.CampaignRecipientId);
    }
}
