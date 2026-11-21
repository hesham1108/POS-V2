using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Barcode).HasMaxLength(64);
        builder.Property(x => x.Unit).HasMaxLength(30).IsRequired();

        builder.Property(x => x.PurchasePrice).HasPrecision(18, 2);
        builder.Property(x => x.AverageCost).HasPrecision(18, 4);
        builder.Property(x => x.RetailMarkupPercent).HasPrecision(18, 4);
        builder.Property(x => x.RetailPrice).HasPrecision(18, 2);
        builder.Property(x => x.WholesaleMarkupPercent).HasPrecision(18, 4);
        builder.Property(x => x.WholesalePrice).HasPrecision(18, 2);
        builder.Property(x => x.StockQuantity).HasPrecision(18, 3);
        builder.Property(x => x.LowStockThreshold).HasPrecision(18, 3);

        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => x.Barcode)
            .IsUnique()
            .HasFilter("[Barcode] IS NOT NULL AND [Barcode] <> ''");

        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => x.NameAr);
        builder.HasIndex(x => x.NameEn);

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
