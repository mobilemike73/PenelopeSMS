using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenelopeSMS.Domain.Entities;

namespace PenelopeSMS.Infrastructure.SqlServer.Configurations;

public sealed class PhoneNumberRecordConfiguration : IEntityTypeConfiguration<PhoneNumberRecord>
{
    public void Configure(EntityTypeBuilder<PhoneNumberRecord> builder)
    {
        builder.ToTable("PhoneNumberRecords");

        builder.HasKey(phoneNumberRecord => phoneNumberRecord.Id);

        builder.Property(phoneNumberRecord => phoneNumberRecord.CanonicalPhoneNumber)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(phoneNumberRecord => phoneNumberRecord.TwilioCountryCode)
            .HasMaxLength(8);

        builder.Property(phoneNumberRecord => phoneNumberRecord.TwilioLineType)
            .HasMaxLength(32);

        builder.Property(phoneNumberRecord => phoneNumberRecord.TwilioCarrierName)
            .HasMaxLength(256);

        builder.Property(phoneNumberRecord => phoneNumberRecord.TwilioMobileCountryCode)
            .HasMaxLength(8);

        builder.Property(phoneNumberRecord => phoneNumberRecord.TwilioMobileNetworkCode)
            .HasMaxLength(8);

        builder.Property(phoneNumberRecord => phoneNumberRecord.LastEnrichmentErrorCode)
            .HasMaxLength(64);

        builder.Property(phoneNumberRecord => phoneNumberRecord.LastEnrichmentErrorMessage)
            .HasMaxLength(1024);

        builder.HasIndex(phoneNumberRecord => phoneNumberRecord.CanonicalPhoneNumber)
            .IsUnique();

        builder.HasMany(phoneNumberRecord => phoneNumberRecord.CustomerPhoneLinks)
            .WithOne(customerPhoneLink => customerPhoneLink.PhoneNumberRecord)
            .HasForeignKey(customerPhoneLink => customerPhoneLink.PhoneNumberRecordId);

        builder.HasMany(phoneNumberRecord => phoneNumberRecord.CampaignRecipients)
            .WithOne(recipient => recipient.PhoneNumberRecord)
            .HasForeignKey(recipient => recipient.PhoneNumberRecordId);
    }
}
