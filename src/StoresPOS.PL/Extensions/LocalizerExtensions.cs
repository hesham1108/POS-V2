using Microsoft.Extensions.Localization;
using StoresPOS.BLL.Exceptions;

namespace StoresPOS.PL.Extensions;

public static class LocalizerExtensions
{
    /// <summary>Translates a business error by its English key and fills its arguments.</summary>
    public static string Localize(this IStringLocalizer localizer, BusinessException ex) =>
        ex.Args.Length == 0
            ? localizer[ex.MessageKey].Value
            : string.Format(localizer[ex.MessageKey].Value, ex.Args);
}
