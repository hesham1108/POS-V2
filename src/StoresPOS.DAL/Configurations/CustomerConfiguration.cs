using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(40);
        builder.Property(x => x.Address).HasMaxLength(400);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.DefaultSaleType).HasConversion<byte>();
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.Phone);
    }
}
