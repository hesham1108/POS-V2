using System.ComponentModel.DataAnnotations;

namespace StoresPOS.PL.ViewModels;

public class CategoryFormViewModel
{
    public int? Id { get; set; }

    [Required]
    [Display(Name = "Arabic name")]
    [StringLength(150)]
    public string NameAr { get; set; } = string.Empty;

    [Required]
    [Display(Name = "English name")]
    [StringLength(150)]
    public string NameEn { get; set; } = string.Empty;

    [Display(Name = "Parent category")]
    public int? ParentCategoryId { get; set; }

    [Display(Name = "Sort order")]
    public int? SortOrder { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public IReadOnlyList<CategoryOptionViewModel> ParentOptions { get; set; } = [];
}

public class CategoryOptionViewModel
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public class CategoryListItemViewModel
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? ParentName { get; set; }
    public int SortOrder { get; set; }
    public int ProductCount { get; set; }
    public int ChildCount { get; set; }
    public bool IsActive { get; set; }
}

public class CategoryReorderViewModel
{
    public int Id { get; set; }
    public int SortOrder { get; set; }
}
