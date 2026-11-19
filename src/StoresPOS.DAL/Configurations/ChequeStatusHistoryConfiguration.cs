using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class ChequeStatusHistoryConfiguration : IEntityTypeConfiguration<ChequeStatusHistory>
{
    public void Configure(EntityTypeBuilder<ChequeStatusHistory> builder)
    {
        builder.ToTable("ChequeStatusHistory");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => new { x.ChequeId, x.CreatedAtUtc });

        builder.HasOne(x => x.Cheque)
            .WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.ChequeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Treasury)
            .WithMany()
            .HasForeignKey(x => x.TreasuryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
