using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Services;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Tests;

public class ShiftServiceTests
{
    private static readonly DateTimeOffset Day1Noon = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Gate_NoShift_IsNoOpenShift()
    {
        await using var db = CreateDb();
        var (mona, _) = await SeedUsersAsync(db);
        var service = new ShiftService(db, new FakeTime(Day1Noon));

        var gate = await service.GetGateAsync(mona);

        Assert.Equal(ShiftGateState.NoOpenShift, gate.State);
        Assert.Null(gate.Shift);
    }

    [Fact]
    public async Task Open_BlankAmount_StartsAtZero_AndOpenerIsParticipant()
    {
        await using var db = CreateDb();
        var (mona, _) = await SeedUsersAsync(db);
        var service = new ShiftService(db, new FakeTime(Day1Noon));

        var shift = await service.OpenAsync(null, mona);

        Assert.Equal(ShiftStatus.Open, shift.Status);
        Assert.Equal(0m, shift.OpeningAmount);
        Assert.Equal(0m, shift.ExpectedCash);
        Assert.Single(shift.Participants);
        Assert.Equal(mona, shift.Participants[0].UserId);

        var gate = await service.GetGateAsync(mona);
        Assert.Equal(ShiftGateState.OpenShift, gate.State);
        Assert.True(gate.IsParticipant);
        Assert.Equal(shift.Id, await service.GetActiveShiftIdForUserAsync(mona));
    }

    [Fact]
    public async Task Open_NegativeAmount_Throws()
    {
        await using var db = CreateDb();
        var (mona, _) = await SeedUsersAsync(db);
        var service = new ShiftService(db, new FakeTime(Day1Noon));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.OpenAsync(-1m, mona));
        Assert.Equal(ShiftService.MsgInvalidOpening, ex.Message);
    }

    [Fact]
    public async Task Open_WhenAlreadyOpen_Throws()
    {
        await using var db = CreateDb();
        var (mona, ahmed) = await SeedUsersAsync(db);
        var service = new ShiftService(db, new FakeTime(Day1Noon));
        await service.OpenAsync(500m, mona);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.OpenAsync(100m, ahmed));
        Assert.Equal(ShiftService.MsgAlreadyOpen, ex.Message);
    }

    [Fact]
    public async Task Resume_SecondCashier_JoinsAsParticipant_Idempotent()
    {
        await using var db = CreateDb();
        var (mona, ahmed) = await SeedUsersAsync(db);
        var service = new ShiftService(db, new FakeTime(Day1Noon));
        var opened = await service.OpenAsync(500m, mona);

        var gate = await service.GetGateAsync(ahmed);
        Assert.Equal(ShiftGateState.OpenShift, gate.State);
        Assert.False(gate.IsParticipant);
        Assert.Null(await service.GetActiveShiftIdForUserAsync(ahmed));

        await service.ResumeAsync(ahmed);
        var resumed = await service.ResumeAsync(ahmed);

        Assert.Equal(opened.Id, resumed.Id);
        Assert.Equal(2, resumed.Participants.Count);
        Assert.Equal(opened.Id, await service.GetActiveShiftIdForUserAsync(ahmed));
    }

    [Fact]
    public async Task Resume_NoOpenShift_Throws()
    {
        await using var db = CreateDb();
        var (mona, _) = await SeedUsersAsync(db);
        var service = new ShiftService(db, new FakeTime(Day1Noon));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.ResumeAsync(mona));
        Assert.Equal(ShiftService.MsgNoOpenShift, ex.Message);
    }

    [Fact]
    public async Task Gate_ShiftFromYesterday_IsOld_AndNotAutoClosed()
    {
        await using var db = CreateDb();
        var (mona, ahmed) = await SeedUsersAsync(db);
        var time = new FakeTime(Day1Noon);
        var service = new ShiftService(db, time);
        await service.OpenAsync(200m, mona);

        time.Now = Day1Noon.AddDays(1);
        var gate = await service.GetGateAsync(ahmed);

        Assert.Equal(ShiftGateState.OldOpenShift, gate.State);
        Assert.True(gate.Shift!.IsOld);
        Assert.Equal(ShiftStatus.Open, gate.Shift.Status);
    }

    [Fact]
    public async Task Close_WithoutCount_IsNotReconciled_NotZeroCounted()
    {
        await using var db = CreateDb();
        var (mona, _) = await SeedUsersAsync(db);
        var service = new ShiftService(db, new FakeTime(Day1Noon));
        var opened = await service.OpenAsync(500m, mona);

        var closed = await service.CloseAsync(new CloseShiftDto { ShiftId = opened.Id, ActualCash = null }, mona);

        Assert.Equal(ShiftStatus.Closed, closed.Status);
        Assert.Equal(ShiftReconciliation.NotReconciled, closed.Reconciliation);
        Assert.Null(closed.ActualCash);
        Assert.Null(closed.CashDifference);
        Assert.Equal(500m, closed.ExpectedCash);
    }

    [Theory]
    [InlineData(500, ShiftReconciliation.Matched, 0)]
    [InlineData(520, ShiftReconciliation.Surplus, 20)]
    [InlineData(450.5, ShiftReconciliation.Shortage, -49.5)]
    public async Task Close_WithCount_ComputesResult(decimal actual, ShiftReconciliation expected, decimal diff)
    {
        await using var db = CreateDb();
        var (mona, _) = await SeedUsersAsync(db);
        var service = new ShiftService(db, new FakeTime(Day1Noon));
        var opened = await service.OpenAsync(500m, mona);

        var closed = await service.CloseAsync(new CloseShiftDto { ShiftId = opened.Id, ActualCash = actual }, mona);

        Assert.Equal(expected, closed.Reconciliation);
        Assert.Equal(diff, closed.CashDifference);
        Assert.Equal(actual, closed.ActualCash);
    }

    [Fact]
    public async Task Close_Twice_Throws_AndNewShiftCanOpen()
    {
        await using var db = CreateDb();
        var (mona, ahmed) = await SeedUsersAsync(db);
        var service = new ShiftService(db, new FakeTime(Day1Noon));
        var opened = await service.OpenAsync(null, mona);
        await service.CloseAsync(new CloseShiftDto { ShiftId = opened.Id }, mona);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.CloseAsync(new CloseShiftDto { ShiftId = opened.Id }, ahmed));
        Assert.Equal(ShiftService.MsgAlreadyClosed, ex.Message);

        Assert.Equal(ShiftGateState.NoOpenShift, (await service.GetGateAsync(ahmed)).State);
        Assert.Null(await service.GetActiveShiftIdForUserAsync(mona));

        var next = await service.OpenAsync(100m, ahmed);
        Assert.NotEqual(opened.Id, next.Id);
    }

    [Fact]
    public async Task Close_NegativeCount_Throws()
    {
        await using var db = CreateDb();
        var (mona, _) = await SeedUsersAsync(db);
        var service = new ShiftService(db, new FakeTime(Day1Noon));
        var opened = await service.OpenAsync(null, mona);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.CloseAsync(new CloseShiftDto { ShiftId = opened.Id, ActualCash = -5m }, mona));
        Assert.Equal(ShiftService.MsgInvalidActual, ex.Message);
    }

    [Fact]
    public void ExpectedCash_OnlyCashCounts()
    {
        // 500 opening + 4,820 cash sales − 120 refunds − 350 cash-outs = 4,850 (proposal §09 example)
        Assert.Equal(4850m, ShiftCalculator.ExpectedCash(500m, 4820m, 120m, 350m));
    }

    private static async Task<(int Mona, int Ahmed)> SeedUsersAsync(ApplicationDbContext db)
    {
        var role = new Role { Name = "Cashier" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var mona = new User { Username = "mona", DisplayName = "Mona", PasswordHash = "h", PasswordSalt = "s", RoleId = role.Id };
        var ahmed = new User { Username = "ahmed", DisplayName = "Ahmed", PasswordHash = "h", PasswordSalt = "s", RoleId = role.Id };
        db.Users.AddRange(mona, ahmed);
        await db.SaveChangesAsync();
        return (mona.Id, ahmed.Id);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
