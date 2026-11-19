using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameEn).HasMaxLength(120).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Location).HasMaxLength(200);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.IsDefault);

        builder.HasData(
            new Warehouse
            {
                Id = 1,
                NameEn = "Main warehouse",
                NameAr = "المخزن الرئيسي",
                Location = null,
                IsActive = true,
                IsDefault = true,
                CreatedAtUtc = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc)
            });
    }
}
