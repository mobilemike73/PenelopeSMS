using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenelopeSMS.Domain.Entities;

namespace PenelopeSMS.Infrastructure.SqlServer.Configurations;

public sealed class CustomerPhoneLinkConfiguration : IEntityTypeConfiguration<CustomerPhoneLink>
{
    public void Configure(EntityTypeBuilder<CustomerPhoneLink> builder)
    {
        builder.ToTable("CustomerPhoneLinks");

        builder.HasKey(customerPhoneLink => customerPhoneLink.Id);

        builder.Property(customerPhoneLink => customerPhoneLink.CustSid)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(customerPhoneLink => customerPhoneLink.RawPhoneNumber)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(customerPhoneLink => new
            {
                customerPhoneLink.CustSid,
                customerPhoneLink.PhoneNumberRecordId
            })
            .IsUnique();

        builder.HasOne(customerPhoneLink => customerPhoneLink.ImportBatch)
            .WithMany(importBatch => importBatch.CustomerPhoneLinks)
            .HasForeignKey(customerPhoneLink => customerPhoneLink.ImportBatchId);
    }
}
