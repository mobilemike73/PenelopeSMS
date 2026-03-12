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

        builder.HasIndex(phoneNumberRecord => phoneNumberRecord.CanonicalPhoneNumber)
            .IsUnique();

        builder.HasMany(phoneNumberRecord => phoneNumberRecord.CustomerPhoneLinks)
            .WithOne(customerPhoneLink => customerPhoneLink.PhoneNumberRecord)
            .HasForeignKey(customerPhoneLink => customerPhoneLink.PhoneNumberRecordId);
    }
}
