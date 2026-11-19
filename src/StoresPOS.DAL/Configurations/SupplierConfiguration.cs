using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(40);
        builder.Property(x => x.Email).HasMaxLength(150);
        builder.Property(x => x.Address).HasMaxLength(400);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => x.NameEn);
        builder.HasIndex(x => x.NameAr);
    }
}
