using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Reconciliation).HasConversion<byte?>();
        builder.Property(x => x.OpeningAmount).HasPrecision(18, 2);
        builder.Property(x => x.ExpectedCash).HasPrecision(18, 2);
        builder.Property(x => x.ActualCash).HasPrecision(18, 2);
        builder.Property(x => x.CashDifference).HasPrecision(18, 2);
        builder.Property(x => x.CloseNotes).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsRowVersion();

        // Business rule 1: at most one open shift for the whole store.
        builder.HasIndex(x => x.Status)
            .IsUnique()
            .HasFilter("[Status] = 1")
            .HasDatabaseName("UX_Shifts_SingleOpen");

        builder.HasIndex(x => x.OpenedAtUtc);

        builder.HasOne(x => x.OpenedByUser)
            .WithMany()
            .HasForeignKey(x => x.OpenedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ClosedByUser)
            .WithMany()
            .HasForeignKey(x => x.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
