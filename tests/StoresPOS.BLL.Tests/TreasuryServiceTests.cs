using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Tests;

public class TreasuryServiceTests
{
    [Fact]
    public async Task Seed_CreatesCashAndBank_DefaultCash()
    {
        await using var db = CreateDb();
        var svc = new TreasuryService(db, TimeProvider.System);
        await svc.EnsureSeededAsync();

        var list = await svc.ListAsync();
        Assert.Equal(2, list.Count);
        Assert.Contains(list, x => x.Type == TreasuryType.Cash && x.IsDefault);
        Assert.Contains(list, x => x.Type == TreasuryType.Bank && !x.IsDefault);
    }

    [Fact]
    public async Task DepositWithdrawTransfer_UpdatesBalance_AndLedger()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var svc = new TreasuryService(db, TimeProvider.System);
        await svc.EnsureSeededAsync();
        var cash = (await svc.ListAsync()).Single(x => x.Type == TreasuryType.Cash);
        var bank = (await svc.ListAsync()).Single(x => x.Type == TreasuryType.Bank);

        await svc.DepositAsync(new TreasuryMoveDto
        {
            TreasuryId = cash.Id,
            Amount = 1000m,
            Reason = "Float"
        }, userId);

        await svc.TransferAsync(new TreasuryTransferDto
        {
            FromTreasuryId = cash.Id,
            ToTreasuryId = bank.Id,
            Amount = 250m,
            Note = "Bank drop"
        }, userId);

        await svc.WithdrawAsync(new TreasuryMoveDto
        {
            TreasuryId = cash.Id,
            Amount = 100m,
            Reason = "Petty cash"
        }, userId);

        var afterCash = await svc.GetAsync(cash.Id);
        var afterBank = await svc.GetAsync(bank.Id);
        Assert.Equal(650m, afterCash!.Balance);
        Assert.Equal(250m, afterBank!.Balance);

        var statement = await svc.GetStatementAsync(new TreasuryStatementFilterDto { TreasuryId = cash.Id });
        Assert.Equal(3, statement.Rows.Count);
        Assert.Equal(650m, statement.ClosingBalance);
    }

    [Fact]
    public async Task Withdraw_Insufficient_Throws()
    {
        await using var db = CreateDb();
        var userId = await SeedUserAsync(db);
        var svc = new TreasuryService(db, TimeProvider.System);
        await svc.EnsureSeededAsync();
        var cash = (await svc.ListAsync()).Single(x => x.IsDefault);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => svc.WithdrawAsync(new TreasuryMoveDto
        {
            TreasuryId = cash.Id,
            Amount = 10m,
            Reason = "Too much"
        }, userId));
        Assert.Equal(TreasuryService.MsgInsufficient, ex.MessageKey);
    }

    [Fact]
    public async Task Deactivate_KeepsHistory_HidesFromActiveList()
    {
        await using var db = CreateDb();
        var svc = new TreasuryService(db, TimeProvider.System);
        await svc.EnsureSeededAsync();
        var bank = (await svc.ListAsync()).Single(x => x.Type == TreasuryType.Bank);

        await svc.DeactivateAsync(bank.Id);
        var active = await svc.ListAsync();
        Assert.DoesNotContain(active, x => x.Id == bank.Id);

        var all = await svc.ListAsync(includeInactive: true);
        Assert.Contains(all, x => x.Id == bank.Id && !x.IsActive);
    }

    private static async Task<int> SeedUserAsync(ApplicationDbContext db)
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
        await db.SaveChangesAsync();
        return db.Users.Single().Id;
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
