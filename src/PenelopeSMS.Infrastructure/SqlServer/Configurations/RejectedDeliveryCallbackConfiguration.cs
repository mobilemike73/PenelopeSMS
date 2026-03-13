using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenelopeSMS.Domain.Entities;

namespace PenelopeSMS.Infrastructure.SqlServer.Configurations;

public sealed class RejectedDeliveryCallbackConfiguration : IEntityTypeConfiguration<RejectedDeliveryCallback>
{
    public void Configure(EntityTypeBuilder<RejectedDeliveryCallback> builder)
    {
        builder.ToTable("RejectedDeliveryCallbacks");

        builder.HasKey(callback => callback.Id);

        builder.Property(callback => callback.RejectionReason)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(callback => callback.CallbackFingerprint)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(callback => callback.RawPayloadJson)
            .IsRequired();

        builder.Property(callback => callback.SignatureHeader)
            .HasMaxLength(512);

        builder.Property(callback => callback.ErrorMessage)
            .HasMaxLength(1024);

        builder.HasIndex(callback => callback.CallbackFingerprint)
            .IsUnique();
    }
}
