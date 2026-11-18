using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.Extensions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Extensions;
using StoresPOS.PL.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddStoresPosDal(builder.Configuration);
builder.Services.AddStoresPosBll(builder.Configuration);
builder.Services.AddStoresPosAuthentication(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    if (!await db.Database.CanConnectAsync())
    {
        throw new InvalidOperationException("Cannot connect to SQL Server database 'StoresPOS'.");
    }

    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    await authService.EnsureSeedDataAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
