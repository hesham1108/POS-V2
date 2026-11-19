using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.Extensions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Extensions;
using StoresPOS.PL;
using StoresPOS.PL.Extensions;
using StoresPOS.PL.Hosting;

var builder = WebApplication.CreateBuilder(args);

var appData = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(appData);
var storeSettingsPath = Path.Combine(appData, "store-settings.json");
var licensePath = Path.Combine(appData, "license.json");
if (!File.Exists(storeSettingsPath))
{
    await File.WriteAllTextAsync(storeSettingsPath, "{}\n");
}

builder.Configuration.AddJsonFile(storeSettingsPath, optional: true, reloadOnChange: true);

builder.Services.Configure<StoreSettingsPersistenceOptions>(options =>
{
    options.FilePath = storeSettingsPath;
    options.LicenseFilePath = licensePath;
});

builder.Services.AddControllersWithViews()
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));
builder.Services.AddStoresPosDal(builder.Configuration);
builder.Services.AddStoresPosBll(builder.Configuration);
builder.Services.AddStoresPosAuthentication(builder.Configuration);
builder.Services.AddStoresPosLocalization();
builder.Services.AddHostedService<DailyBackupHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        // Integration tests swap in the EF InMemory provider.
        await db.Database.EnsureCreatedAsync();
    }

    if (!await db.Database.CanConnectAsync())
    {
        throw new InvalidOperationException("Cannot connect to SQL Server database 'StoresPOS'.");
    }

    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    await authService.EnsureSeedDataAsync();

    var paymentMethods = scope.ServiceProvider.GetRequiredService<IPaymentMethodService>();
    await paymentMethods.EnsureSeededAsync();

    var treasuries = scope.ServiceProvider.GetRequiredService<ITreasuryService>();
    await treasuries.EnsureSeededAsync();

    var warehouses = scope.ServiceProvider.GetRequiredService<IWarehouseService>();
    await warehouses.EnsureSeededAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program;
