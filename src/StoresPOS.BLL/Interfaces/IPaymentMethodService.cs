using StoresPOS.BLL.DTOs;
using StoresPOS.DAL.Entities;

namespace StoresPOS.BLL.Interfaces;

public interface IPaymentMethodService
{
    Task EnsureSeededAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentMethodDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentMethodDto>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task<PaymentMethodDto?> GetAsync(byte id, CancellationToken cancellationToken = default);

    Task<bool> IsCashAsync(byte id, CancellationToken cancellationToken = default);

    Task<PaymentMethodKind?> GetKindAsync(byte id, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<byte, PaymentMethodDto>> GetLookupAsync(CancellationToken cancellationToken = default);

    Task<PaymentMethodDto> CreateAsync(PaymentMethodFormDto form, CancellationToken cancellationToken = default);

    Task<PaymentMethodDto> UpdateAsync(PaymentMethodFormDto form, CancellationToken cancellationToken = default);

    Task DeactivateAsync(byte id, CancellationToken cancellationToken = default);

    Task ActivateAsync(byte id, CancellationToken cancellationToken = default);

    Task ReorderAsync(IReadOnlyList<PaymentMethodReorderItemDto> items, CancellationToken cancellationToken = default);
}
