using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class SalesInvoiceItemConfiguration : IEntityTypeConfiguration<SalesInvoiceItem>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceItem> builder)
    {
        builder.ToTable("SalesInvoiceItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductNameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProductNameEn).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Barcode).HasMaxLength(64);
        builder.Property(x => x.Unit).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.CatalogUnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.UnitCost).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);

        builder.HasIndex(x => x.ProductId);

        builder.HasOne(x => x.SalesInvoice)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PriceOverriddenByUser)
            .WithMany()
            .HasForeignKey(x => x.PriceOverriddenByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
