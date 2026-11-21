using System.Text.Json;

namespace StoresPOS.BLL.Models;

/// <summary>
/// One mapper for store settings (Phase 19). Settings used to be copied field by field in six
/// places (service clone, JSON writer, controller ToForm/ToDto, receipts clone), so every new field
/// had to be added six times. Mapping by property name through JSON copies every field — including
/// nested ones like <see cref="DocumentPrintSettings"/> — and always returns a deep copy.
/// </summary>
public static class SettingsMapper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static TTarget Map<TTarget>(object source)
        where TTarget : new()
    {
        ArgumentNullException.ThrowIfNull(source);
        var json = JsonSerializer.SerializeToUtf8Bytes(source, source.GetType(), Options);
        return JsonSerializer.Deserialize<TTarget>(json, Options) ?? new TTarget();
    }

    public static StoreSettings Clone(StoreSettings source) => Map<StoreSettings>(source);
}
