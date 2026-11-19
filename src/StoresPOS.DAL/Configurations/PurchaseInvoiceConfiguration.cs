using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> builder)
    {
        builder.ToTable("PurchaseInvoices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InvoiceNumber).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();

        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.SubTotal).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<byte>();

        builder.HasIndex(x => x.SupplierId);
        builder.HasIndex(x => x.InvoiceDate);
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.Supplier)
            .WithMany(x => x.PurchaseInvoices)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ConfirmedByUser)
            .WithMany()
            .HasForeignKey(x => x.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PaidFromTreasury)
            .WithMany()
            .HasForeignKey(x => x.PaidFromTreasuryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
