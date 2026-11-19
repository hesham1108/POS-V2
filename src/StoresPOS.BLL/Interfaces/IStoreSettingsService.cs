using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Models;

namespace StoresPOS.BLL.Interfaces;

public interface IStoreSettingsService
{
    StoreSettings Current { get; }

    Task<StoreSettings> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(StoreSettingsFormDto form, CancellationToken cancellationToken = default);
}
