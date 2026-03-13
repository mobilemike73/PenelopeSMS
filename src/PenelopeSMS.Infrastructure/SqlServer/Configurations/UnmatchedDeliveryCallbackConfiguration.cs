using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenelopeSMS.Domain.Entities;

namespace PenelopeSMS.Infrastructure.SqlServer.Configurations;

public sealed class UnmatchedDeliveryCallbackConfiguration : IEntityTypeConfiguration<UnmatchedDeliveryCallback>
{
    public void Configure(EntityTypeBuilder<UnmatchedDeliveryCallback> builder)
    {
        builder.ToTable("UnmatchedDeliveryCallbacks");

        builder.HasKey(callback => callback.Id);

        builder.Property(callback => callback.TwilioMessageSid)
            .HasMaxLength(64);

        builder.Property(callback => callback.MessageStatus)
            .HasMaxLength(64);

        builder.Property(callback => callback.CallbackFingerprint)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(callback => callback.ProviderErrorCode)
            .HasMaxLength(64);

        builder.Property(callback => callback.ProviderErrorMessage)
            .HasMaxLength(1024);

        builder.Property(callback => callback.ProviderEventRawValue)
            .HasMaxLength(128);

        builder.Property(callback => callback.RawPayloadJson)
            .IsRequired();

        builder.HasIndex(callback => callback.CallbackFingerprint)
            .IsUnique();
    }
}
