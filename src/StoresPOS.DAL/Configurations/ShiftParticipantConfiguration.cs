using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class ShiftParticipantConfiguration : IEntityTypeConfiguration<ShiftParticipant>
{
    public void Configure(EntityTypeBuilder<ShiftParticipant> builder)
    {
        builder.ToTable("ShiftParticipants");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ShiftId, x.UserId }).IsUnique();

        builder.HasOne(x => x.Shift)
            .WithMany(x => x.Participants)
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
