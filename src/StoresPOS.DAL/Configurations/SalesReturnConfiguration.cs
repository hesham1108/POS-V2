using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class SalesReturnConfiguration : IEntityTypeConfiguration<SalesReturn>
{
    public void Configure(EntityTypeBuilder<SalesReturn> builder)
    {
        builder.ToTable("SalesReturns");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReturnNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RefundMethod).HasConversion<byte>();
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.Reason).HasMaxLength(300).IsRequired();

        builder.HasIndex(x => x.ReturnSequence).IsUnique();
        builder.HasIndex(x => x.ReturnNumber).IsUnique();
        builder.HasIndex(x => x.OriginalSalesInvoiceId);
        builder.HasIndex(x => x.ShiftId);
        builder.HasIndex(x => x.CreatedAtUtc);

        builder.HasOne(x => x.OriginalSalesInvoice)
            .WithMany()
            .HasForeignKey(x => x.OriginalSalesInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Shift)
            .WithMany()
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
