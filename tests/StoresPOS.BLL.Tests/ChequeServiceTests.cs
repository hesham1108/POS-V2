using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Tests;

public class ChequeServiceTests
{
    [Fact]
    public async Task CreateIncoming_Collect_WritesTreasuryIn()
    {
        await using var db = CreateDb();
        var (userId, customerId, treasuryId) = await SeedAsync(db);
        var time = TimeProvider.System;
        var treasuries = new TreasuryService(db, time);
        var cheques = new ChequeService(db, treasuries, time);

        var created = await cheques.CreateAsync(new ChequeFormDto
        {
            Number = "CH-100",
            Bank = "NBE",
            DueDate = DateTime.UtcNow.Date.AddDays(7),
            Amount = 500m,
            Direction = ChequeDirection.In,
            CustomerId = customerId
        }, userId);

        Assert.Equal(ChequeStatus.Pending, created.Status);

        var collected = await cheques.ChangeStatusAsync(new ChequeStatusChangeDto
        {
            ChequeId = created.Id,
            NewStatus = ChequeStatus.Collected,
            TreasuryId = treasuryId
        }, userId);

        Assert.Equal(ChequeStatus.Collected, collected.Status);
        var balance = (await treasuries.GetAsync(treasuryId))!.Balance;
        Assert.Equal(500m, balance);
        Assert.Equal(1, await db.TreasuryTransactions.CountAsync(x =>
            x.SourceType == TreasuryTransactionSourceType.Cheque
            && x.Direction == TreasuryTransactionDirection.In));
    }

    [Fact]
    public async Task Bounce_DoesNotMoveMoney()
    {
        await using var db = CreateDb();
        var (userId, customerId, treasuryId) = await SeedAsync(db);
        var time = TimeProvider.System;
        var treasuries = new TreasuryService(db, time);
        var cheques = new ChequeService(db, treasuries, time);

        var created = await cheques.CreateAsync(new ChequeFormDto
        {
            Number = "CH-200",
            DueDate = DateTime.UtcNow.Date,
            Amount = 100m,
            Direction = ChequeDirection.In,
            CustomerId = customerId
        }, userId);

        await cheques.ChangeStatusAsync(new ChequeStatusChangeDto
        {
            ChequeId = created.Id,
            NewStatus = ChequeStatus.Bounced
        }, userId);

        Assert.Equal(0m, (await treasuries.GetAsync(treasuryId))!.Balance);
        Assert.Empty(db.TreasuryTransactions);
    }

    [Fact]
    public async Task Outgoing_Paid_WritesTreasuryOut()
    {
        await using var db = CreateDb();
        var (userId, _, _) = await SeedAsync(db);
        var time = TimeProvider.System;
        var treasuries = new TreasuryService(db, time);
        await treasuries.EnsureSeededAsync();
        var cash = (await treasuries.ListAsync()).Single(x => x.IsDefault);
        await treasuries.DepositAsync(new TreasuryMoveDto
        {
            TreasuryId = cash.Id,
            Amount = 1000m,
            Reason = "Float"
        }, userId);

        db.Suppliers.Add(new Supplier { NameAr = "مورد", NameEn = "Supplier", IsActive = true });
        await db.SaveChangesAsync();
        var supplierId = db.Suppliers.Single().Id;

        var cheques = new ChequeService(db, treasuries, time);
        var created = await cheques.CreateAsync(new ChequeFormDto
        {
            Number = "OUT-1",
            DueDate = DateTime.UtcNow.Date.AddDays(3),
            Amount = 250m,
            Direction = ChequeDirection.Out,
            SupplierId = supplierId
        }, userId);

        await cheques.ChangeStatusAsync(new ChequeStatusChangeDto
        {
            ChequeId = created.Id,
            NewStatus = ChequeStatus.Paid,
            TreasuryId = cash.Id
        }, userId);

        Assert.Equal(750m, (await treasuries.GetAsync(cash.Id))!.Balance);
    }

    [Fact]
    public async Task InvalidTransition_Throws()
    {
        await using var db = CreateDb();
        var (userId, customerId, _) = await SeedAsync(db);
        var time = TimeProvider.System;
        var cheques = new ChequeService(db, new TreasuryService(db, time), time);
        var created = await cheques.CreateAsync(new ChequeFormDto
        {
            Number = "X",
            DueDate = DateTime.UtcNow.Date,
            Amount = 10m,
            Direction = ChequeDirection.In,
            CustomerId = customerId
        }, userId);

        await Assert.ThrowsAsync<BusinessException>(() => cheques.ChangeStatusAsync(new ChequeStatusChangeDto
        {
            ChequeId = created.Id,
            NewStatus = ChequeStatus.Paid
        }, userId));
    }

    private static async Task<(int UserId, int CustomerId, int TreasuryId)> SeedAsync(ApplicationDbContext db)
    {
        db.Roles.Add(new Role { Name = "Admin" });
        await db.SaveChangesAsync();
        db.Users.Add(new User
        {
            Username = "admin",
            DisplayName = "Admin",
            PasswordHash = "h",
            PasswordSalt = "s",
            RoleId = db.Roles.Single().Id,
            IsActive = true
        });
        db.Customers.Add(new Customer
        {
            Name = "Ali",
            IsActive = true,
            DefaultSaleType = SaleType.Retail
        });
        db.Treasuries.Add(new Treasury
        {
            NameEn = "Main",
            NameAr = "رئيسية",
            Type = TreasuryType.Cash,
            OpeningBalance = 0m,
            IsActive = true,
            IsDefault = true
        });
        await db.SaveChangesAsync();
        return (db.Users.Single().Id, db.Customers.Single().Id, db.Treasuries.Single().Id);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
