using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActorName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(60).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Details).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => new { x.TargetUserId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.ActorUserId, x.CreatedAtUtc });
    }
}
