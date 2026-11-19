using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class TreasuryConfiguration : IEntityTypeConfiguration<Treasury>
{
    public void Configure(EntityTypeBuilder<Treasury> builder)
    {
        builder.ToTable("Treasuries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameEn).HasMaxLength(120).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.OpeningBalance).HasPrecision(18, 2);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.IsDefault);

        builder.HasData(
            new Treasury
            {
                Id = 1,
                NameEn = "Main cash",
                NameAr = "الخزنة النقدية",
                Type = TreasuryType.Cash,
                OpeningBalance = 0m,
                IsActive = true,
                IsDefault = true,
                CreatedAtUtc = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc)
            },
            new Treasury
            {
                Id = 2,
                NameEn = "Main bank",
                NameAr = "الحساب البنكي",
                Type = TreasuryType.Bank,
                OpeningBalance = 0m,
                IsActive = true,
                IsDefault = false,
                CreatedAtUtc = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc)
            });
    }
}
