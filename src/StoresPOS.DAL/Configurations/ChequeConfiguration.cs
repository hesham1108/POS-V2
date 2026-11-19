using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class ChequeConfiguration : IEntityTypeConfiguration<Cheque>
{
    public void Configure(EntityTypeBuilder<Cheque> builder)
    {
        builder.ToTable("Cheques");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Bank).HasMaxLength(120);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Direction).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => x.Direction);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.DueDate);
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.SupplierId);
        builder.HasIndex(x => x.Number);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SalesInvoice)
            .WithMany()
            .HasForeignKey(x => x.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PurchaseInvoice)
            .WithMany()
            .HasForeignKey(x => x.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SettledTreasury)
            .WithMany()
            .HasForeignKey(x => x.SettledTreasuryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
