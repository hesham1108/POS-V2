using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class TreasuryTransactionConfiguration : IEntityTypeConfiguration<TreasuryTransaction>
{
    public void Configure(EntityTypeBuilder<TreasuryTransaction> builder)
    {
        builder.ToTable("TreasuryTransactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Direction).HasConversion<byte>();
        builder.Property(x => x.SourceType).HasConversion<byte>();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => new { x.TreasuryId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.SourceType, x.SourceId });
        builder.HasIndex(x => x.TransferGroupId);

        builder.HasOne(x => x.Treasury)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.TreasuryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}