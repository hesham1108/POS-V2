using Microsoft.Extensions.Options;
using StoresPOS.BLL.Models;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;

namespace StoresPOS.BLL.Tests;

internal static class TestFactories
{
    public static InventoryService Inventory(ApplicationDbContext db) => new(db);

    public static WarehouseService Warehouses(ApplicationDbContext db, TimeProvider? time = null)
    {
        var inventory = Inventory(db);
        return new WarehouseService(
            db,
            inventory,
            Options.Create(new StoreSettings()),
            time ?? TimeProvider.System);
    }

    public static PurchaseService Purchases(ApplicationDbContext db, TimeProvider? time = null)
    {
        var inventory = Inventory(db);
        var treasuries = new TreasuryService(db, time ?? TimeProvider.System);
        var warehouses = new WarehouseService(
            db, inventory, Options.Create(new StoreSettings()), time ?? TimeProvider.System);
        return new PurchaseService(
            db,
            inventory,
            treasuries,
            new ChequeService(db, treasuries, time ?? TimeProvider.System),
            warehouses);
    }

    public static async Task<int> DefaultWarehouseIdAsync(ApplicationDbContext db)
    {
        var wh = Warehouses(db);
        await wh.EnsureSeededAsync();
        return await wh.GetSellingWarehouseIdAsync();
    }
}
