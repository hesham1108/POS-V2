using Microsoft.EntityFrameworkCore;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Interfaces;
using StoresPOS.DAL.Context;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Services;

public class PaymentMethodService : IPaymentMethodService
{
    public const string MsgNotFound = "Payment method not found.";
    public const string MsgNameRequired = "English and Arabic names are required.";
    public const string MsgCannotDelete = "Payment methods cannot be deleted once used. Deactivate instead.";

    private readonly ApplicationDbContext _db;

    public PaymentMethodService(ApplicationDbContext db) => _db = db;

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        // HasData seeds via migration; this is a safety net for InMemory tests / EnsureCreated.
        if (await _db.PaymentMethods.AnyAsync(cancellationToken))
        {
            return;
        }

        _db.PaymentMethods.AddRange(
            new PaymentMethodDefinition
            {
                Id = (byte)PaymentMethod.Cash, NameEn = "Cash", NameAr = "نقدي",
                Kind = PaymentMethodKind.Cash, IsActive = true, SortOrder = 10
            },
            new PaymentMethodDefinition
            {
                Id = (byte)PaymentMethod.Card, NameEn = "Visa / Card", NameAr = "فيزا / بطاقة",
                Kind = PaymentMethodKind.Card, IsActive = true, SortOrder = 20
            },
            new PaymentMethodDefinition
            {
                Id = (byte)PaymentMethod.InstaPay, NameEn = "InstaPay", NameAr = "إنستا باي",
                Kind = PaymentMethodKind.Wallet, IsActive = true, SortOrder = 30
            },
            new PaymentMethodDefinition
            {
                Id = (byte)PaymentMethod.VodafoneCash, NameEn = "Vodafone Cash", NameAr = "فودافون كاش",
                Kind = PaymentMethodKind.Wallet, IsActive = true, SortOrder = 40
            },
            new PaymentMethodDefinition
            {
                Id = (byte)PaymentMethod.Cheque, NameEn = "Cheque", NameAr = "شيك",
                Kind = PaymentMethodKind.Cheque, IsActive = true, SortOrder = 50
            });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentMethodDto>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var query = _db.PaymentMethods.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<PaymentMethodDto>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        ListAsync(includeInactive: false, cancellationToken);

    public async Task<PaymentMethodDto?> GetAsync(byte id, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var row = await _db.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<bool> IsCashAsync(byte id, CancellationToken cancellationToken = default)
    {
        var kind = await GetKindAsync(id, cancellationToken);
        return kind == PaymentMethodKind.Cash;
    }

    public async Task<PaymentMethodKind?> GetKindAsync(byte id, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        return await _db.PaymentMethods.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => (PaymentMethodKind?)x.Kind)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<byte, PaymentMethodDto>> GetLookupAsync(
        CancellationToken cancellationToken = default)
    {
        var list = await ListAsync(includeInactive: true, cancellationToken);
        return list.ToDictionary(x => x.Id);
    }

    public async Task<PaymentMethodDto> CreateAsync(
        PaymentMethodFormDto form,
        CancellationToken cancellationToken = default)
    {
        ValidateNames(form);
        await EnsureSeededAsync(cancellationToken);

        var nextId = (byte)(await _db.PaymentMethods.MaxAsync(x => (int)x.Id, cancellationToken) + 1);
        if (nextId == 0)
        {
            throw new BusinessException("No more payment method ids available.");
        }

        var sort = form.SortOrder > 0
            ? form.SortOrder
            : await _db.PaymentMethods.MaxAsync(x => x.SortOrder, cancellationToken) + 10;

        var row = new PaymentMethodDefinition
        {
            Id = nextId,
            NameEn = form.NameEn.Trim(),
            NameAr = form.NameAr.Trim(),
            Kind = form.Kind,
            IsActive = form.IsActive,
            SortOrder = sort,
            DefaultTreasuryId = form.DefaultTreasuryId
        };
        _db.PaymentMethods.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async Task<PaymentMethodDto> UpdateAsync(
        PaymentMethodFormDto form,
        CancellationToken cancellationToken = default)
    {
        if (!form.Id.HasValue)
        {
            throw new BusinessException(MsgNotFound);
        }

        ValidateNames(form);
        var row = await _db.PaymentMethods.FirstOrDefaultAsync(x => x.Id == form.Id.Value, cancellationToken)
            ?? throw new BusinessException(MsgNotFound);

        row.NameEn = form.NameEn.Trim();
        row.NameAr = form.NameAr.Trim();
        row.Kind = form.Kind;
        row.IsActive = form.IsActive;
        if (form.SortOrder > 0)
        {
            row.SortOrder = form.SortOrder;
        }

        row.DefaultTreasuryId = form.DefaultTreasuryId;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async Task DeactivateAsync(byte id, CancellationToken cancellationToken = default)
    {
        var row = await _db.PaymentMethods.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException(MsgNotFound);
        row.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ActivateAsync(byte id, CancellationToken cancellationToken = default)
    {
        var row = await _db.PaymentMethods.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BusinessException(MsgNotFound);
        row.IsActive = true;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderAsync(
        IReadOnlyList<PaymentMethodReorderItemDto> items,
        CancellationToken cancellationToken = default)
    {
        var ids = items.Select(x => x.Id).ToList();
        var rows = await _db.PaymentMethods.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            var row = rows.FirstOrDefault(x => x.Id == item.Id);
            if (row is not null)
            {
                row.SortOrder = item.SortOrder;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateNames(PaymentMethodFormDto form)
    {
        if (string.IsNullOrWhiteSpace(form.NameEn) || string.IsNullOrWhiteSpace(form.NameAr))
        {
            throw new BusinessException(MsgNameRequired);
        }
    }

    private static PaymentMethodDto Map(PaymentMethodDefinition x) => new()
    {
        Id = x.Id,
        NameEn = x.NameEn,
        NameAr = x.NameAr,
        Kind = x.Kind,
        IsActive = x.IsActive,
        SortOrder = x.SortOrder,
        DefaultTreasuryId = x.DefaultTreasuryId
    };
}
