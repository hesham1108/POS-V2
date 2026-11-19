using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Tests;

public class PaymentMethodServiceTests
{
    [Fact]
    public async Task Seed_KeepsLegacyIds_AndAddsCheque()
    {
        await using var db = CreateDb();
        var svc = new PaymentMethodService(db);
        await svc.EnsureSeededAsync();

        var list = await svc.ListAsync(includeInactive: true);
        Assert.Equal(5, list.Count);
        Assert.Contains(list, x => x.Id == (byte)PaymentMethod.Cash && x.IsCash);
        Assert.Contains(list, x => x.Id == (byte)PaymentMethod.Card);
        Assert.Contains(list, x => x.Id == (byte)PaymentMethod.InstaPay);
        Assert.Contains(list, x => x.Id == (byte)PaymentMethod.VodafoneCash);
        Assert.Contains(list, x => x.Id == (byte)PaymentMethod.Cheque && x.Kind == PaymentMethodKind.Cheque);
    }

    [Fact]
    public async Task Inactive_HiddenFromActiveList_StillResolvable()
    {
        await using var db = CreateDb();
        var svc = new PaymentMethodService(db);
        await svc.EnsureSeededAsync();
        await svc.DeactivateAsync((byte)PaymentMethod.InstaPay);

        var active = await svc.ListActiveAsync();
        Assert.DoesNotContain(active, x => x.Id == (byte)PaymentMethod.InstaPay);

        var row = await svc.GetAsync((byte)PaymentMethod.InstaPay);
        Assert.NotNull(row);
        Assert.False(row!.IsActive);
    }

    [Fact]
    public async Task Create_CustomMethod_GetsNewId()
    {
        await using var db = CreateDb();
        var svc = new PaymentMethodService(db);
        await svc.EnsureSeededAsync();

        var created = await svc.CreateAsync(new PaymentMethodFormDto
        {
            NameEn = "Bank transfer",
            NameAr = "تحويل بنكي",
            Kind = PaymentMethodKind.BankTransfer,
            IsActive = true
        });

        Assert.True(created.Id >= 6);
        Assert.Equal(PaymentMethodKind.BankTransfer, created.Kind);
        Assert.False(created.IsCash);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
