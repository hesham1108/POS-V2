using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.ToTable("UserPermissions");
        builder.HasKey(x => new { x.UserId, x.Permission });
        builder.Property(x => x.Permission).HasMaxLength(100).IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(x => x.Permissions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
