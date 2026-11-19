using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MovementType).HasConversion<byte>();
        builder.Property(x => x.QuantityChange).HasPrecision(18, 3);
        builder.Property(x => x.QuantityBefore).HasPrecision(18, 3);
        builder.Property(x => x.QuantityAfter).HasPrecision(18, 3);
        builder.Property(x => x.ReferenceType).HasMaxLength(50);
        builder.Property(x => x.Reason).HasMaxLength(500);

        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.MovementType);
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
