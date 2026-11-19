using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Configurations;

public class PaymentMethodDefinitionConfiguration : IEntityTypeConfiguration<PaymentMethodDefinition>
{
    public void Configure(EntityTypeBuilder<PaymentMethodDefinition> builder)
    {
        builder.ToTable("PaymentMethods");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.NameEn).HasMaxLength(80).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.HasIndex(x => x.SortOrder);
        builder.HasIndex(x => x.IsActive);

        builder.HasOne(x => x.DefaultTreasury)
            .WithMany()
            .HasForeignKey(x => x.DefaultTreasuryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasData(
            new PaymentMethodDefinition
            {
                Id = (byte)PaymentMethod.Cash,
                NameEn = "Cash",
                NameAr = "نقدي",
                Kind = PaymentMethodKind.Cash,
                IsActive = true,
                SortOrder = 10
            },
            new PaymentMethodDefinition
            {
                Id = (byte)PaymentMethod.Card,
                NameEn = "Visa / Card",
                NameAr = "فيزا / بطاقة",
                Kind = PaymentMethodKind.Card,
                IsActive = true,
                SortOrder = 20
            },
            new PaymentMethodDefinition
            {
                Id = (byte)PaymentMethod.InstaPay,
                NameEn = "InstaPay",
                NameAr = "إنستا باي",
                Kind = PaymentMethodKind.Wallet,
                IsActive = true,
                SortOrder = 30
            },
            new PaymentMethodDefinition
            {
                Id = (byte)PaymentMethod.VodafoneCash,
                NameEn = "Vodafone Cash",
                NameAr = "فودافون كاش",
                Kind = PaymentMethodKind.Wallet,
                IsActive = true,
                SortOrder = 40
            },
            new PaymentMethodDefinition
            {
                Id = (byte)PaymentMethod.Cheque,
                NameEn = "Cheque",
                NameAr = "شيك",
                Kind = PaymentMethodKind.Cheque,
                IsActive = true,
                SortOrder = 50
            });
    }
}
