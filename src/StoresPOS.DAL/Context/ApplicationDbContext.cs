using Microsoft.EntityFrameworkCore;
using StoresPOS.DAL.Entities;

namespace StoresPOS.DAL.Context;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems => Set<PurchaseInvoiceItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftParticipant> ShiftParticipants => Set<ShiftParticipant>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceItem> SalesInvoiceItems => Set<SalesInvoiceItem>();
    public DbSet<CashOut> CashOuts => Set<CashOut>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnItem> SalesReturnItems => Set<SalesReturnItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<PaymentMethodDefinition> PaymentMethods => Set<PaymentMethodDefinition>();
    public DbSet<Treasury> Treasuries => Set<Treasury>();
    public DbSet<TreasuryTransaction> TreasuryTransactions => Set<TreasuryTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
