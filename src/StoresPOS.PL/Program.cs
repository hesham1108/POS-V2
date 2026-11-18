using Microsoft.EntityFrameworkCore;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddStoresPosDal(builder.Configuration);

var app = builder.Build();

// Verify database connection at startup (creates empty DB if missing).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
    if (!db.Database.CanConnect())
    {
        throw new InvalidOperationException("Cannot connect to SQL Server database 'StoresPOS'.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
