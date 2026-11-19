using StoresPOS.BLL.DTOs;

namespace StoresPOS.PL.ViewModels;

public class PosPageViewModel
{
    public CartDto Cart { get; set; } = new();
    public IReadOnlyList<PosCategoryDto> Categories { get; set; } = [];
    public IReadOnlyList<PosProductDto> Products { get; set; } = [];
    public int ShiftId { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public bool CanOverridePrice { get; set; }
    public bool CanDiscount { get; set; }
    public bool CanCloseShift { get; set; }
    public bool CanSeeDashboard { get; set; }
}

public class PosCartViewModel
{
    public CartDto Cart { get; set; } = new();
    public bool CanOverridePrice { get; set; }
    public bool CanDiscount { get; set; }
}
