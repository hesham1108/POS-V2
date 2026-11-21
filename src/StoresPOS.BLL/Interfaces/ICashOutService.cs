using StoresPOS.BLL.DTOs;

namespace StoresPOS.BLL.Interfaces;

/// <summary>Journey E: cash taken out of the drawer during the open shift.</summary>
public interface ICashOutService
{
    /// <summary>Records a cash-out on the user's open shift. Over the expected drawer = warning, not blocked.</summary>
    Task<CashOutResultDto> CreateAsync(CreateCashOutDto dto, int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CashOutListItemDto>> GetListAsync(CashOutFilterDto? filter = null, CancellationToken cancellationToken = default);

    Task<PagedResult<CashOutListItemDto>> GetPageAsync(CashOutFilterDto filter, ListQuery list, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(int UserId, string DisplayName)>> GetUsersAsync(CashOutFilterDto filter, CancellationToken cancellationToken = default);
}
