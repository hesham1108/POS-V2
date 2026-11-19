using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.BLL.Models;
using StoresPOS.BLL.Services;

namespace StoresPOS.BLL.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddStoresPosBll(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<AdminSeedSettings>(configuration.GetSection(AdminSeedSettings.SectionName));
        services.Configure<StoreSettings>(configuration.GetSection(StoreSettings.SectionName));

        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<ICashOutService, CashOutService>();
        services.AddScoped<IReturnService, ReturnService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
