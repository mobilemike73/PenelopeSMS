using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenelopeSMS.Domain.Entities;

namespace PenelopeSMS.Infrastructure.SqlServer.Configurations;

public sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("ImportBatches");

        builder.HasKey(importBatch => importBatch.Id);

        builder.Property(importBatch => importBatch.Status)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(importBatch => importBatch.RowsRead)
            .HasDefaultValue(0);

        builder.Property(importBatch => importBatch.RowsImported)
            .HasDefaultValue(0);

        builder.Property(importBatch => importBatch.RowsRejected)
            .HasDefaultValue(0);
    }
}
