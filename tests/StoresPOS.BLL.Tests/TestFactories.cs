using Microsoft.Extensions.Options;
using StoresPOS.BLL.Models;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;

namespace StoresPOS.BLL.Tests;

internal static class TestFactories
{
    public static IOptionsMonitor<StoreSettings> StoreMonitor(StoreSettings? settings = null) =>
        new TestOptionsMonitor<StoreSettings>(settings ?? new StoreSettings());

    public static InventoryService Inventory(ApplicationDbContext db) => new(db);

    public static WarehouseService Warehouses(ApplicationDbContext db, TimeProvider? time = null)
    {
        var inventory = Inventory(db);
        return new WarehouseService(
            db,
            inventory,
            StoreMonitor(),
            time ?? TimeProvider.System);
    }

    public static PurchaseService Purchases(ApplicationDbContext db, TimeProvider? time = null)
    {
        var inventory = Inventory(db);
        var treasuries = new TreasuryService(db, time ?? TimeProvider.System);
        var warehouses = new WarehouseService(
            db, inventory, StoreMonitor(), time ?? TimeProvider.System);
        return new PurchaseService(
            db,
            inventory,
            treasuries,
            new ChequeService(db, treasuries, time ?? TimeProvider.System),
            warehouses);
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T current) => CurrentValue = current;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    public static async Task<int> DefaultWarehouseIdAsync(ApplicationDbContext db)
    {
        var wh = Warehouses(db);
        await wh.EnsureSeededAsync();
        return await wh.GetSellingWarehouseIdAsync();
    }
}
