using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenelopeSMS.Domain.Entities;

namespace PenelopeSMS.Infrastructure.SqlServer.Configurations;

public sealed class CampaignRecipientConfiguration : IEntityTypeConfiguration<CampaignRecipient>
{
    public void Configure(EntityTypeBuilder<CampaignRecipient> builder)
    {
        builder.ToTable("CampaignRecipients");

        builder.HasKey(recipient => recipient.Id);

        builder.Property(recipient => recipient.TwilioMessageSid)
            .HasMaxLength(64);

        builder.Property(recipient => recipient.InitialTwilioStatus)
            .HasMaxLength(64);

        builder.Property(recipient => recipient.ProviderErrorCode)
            .HasMaxLength(64);

        builder.Property(recipient => recipient.ProviderErrorMessage)
            .HasMaxLength(1024);

        builder.HasIndex(recipient => new
            {
                recipient.CampaignId,
                recipient.PhoneNumberRecordId
            })
            .IsUnique();

        builder.HasOne(recipient => recipient.PhoneNumberRecord)
            .WithMany(phoneNumberRecord => phoneNumberRecord.CampaignRecipients)
            .HasForeignKey(recipient => recipient.PhoneNumberRecordId);
    }
}
