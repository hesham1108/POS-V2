using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.ToTable("SalesInvoices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.SaleType).HasConversion<byte>();
        builder.Property(x => x.PaymentMethod).HasConversion<byte?>();
        builder.Property(x => x.InvoiceNumber).HasMaxLength(20);
        builder.Property(x => x.PaymentReference).HasMaxLength(100);
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.Total).HasPrecision(18, 2);
        builder.Property(x => x.AmountPaid).HasPrecision(18, 2);
        builder.Property(x => x.ChangeAmount).HasPrecision(18, 2);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.InvoiceSequence).IsUnique().HasFilter("[InvoiceSequence] IS NOT NULL");
        builder.HasIndex(x => x.InvoiceNumber).IsUnique().HasFilter("[InvoiceNumber] IS NOT NULL");

        // One live cart per cashier.
        builder.HasIndex(x => x.CashierUserId)
            .IsUnique()
            .HasFilter("[Status] = 1")
            .HasDatabaseName("UX_SalesInvoices_OneDraftPerCashier");

        builder.HasIndex(x => new { x.ShiftId, x.Status });
        builder.HasIndex(x => x.CompletedAtUtc);

        builder.HasOne(x => x.Shift)
            .WithMany()
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CashierUser)
            .WithMany()
            .HasForeignKey(x => x.CashierUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DiscountByUser)
            .WithMany()
            .HasForeignKey(x => x.DiscountByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
