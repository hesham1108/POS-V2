using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class SalesReturnItemConfiguration : IEntityTypeConfiguration<SalesReturnItem>
{
    public void Configure(EntityTypeBuilder<SalesReturnItem> builder)
    {
        builder.ToTable("SalesReturnItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductNameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProductNameEn).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.RefundAmount).HasPrecision(18, 2);

        builder.HasIndex(x => x.SalesInvoiceItemId);

        builder.HasOne(x => x.SalesReturn)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.SalesReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SalesInvoiceItem)
            .WithMany()
            .HasForeignKey(x => x.SalesInvoiceItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
