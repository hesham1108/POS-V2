namespace StoresPOS.BLL.DTOs;

public class CategoryDto
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int? ParentCategoryId { get; set; }
    public string? ParentNameAr { get; set; }
    public string? ParentNameEn { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public int ProductCount { get; set; }
    public int ChildCount { get; set; }
}

public class CategoryTreeNodeDto
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int? ParentCategoryId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public List<CategoryTreeNodeDto> Children { get; set; } = [];
}

public class CategoryUpsertDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int? ParentCategoryId { get; set; }
    public int? SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CategoryReorderItemDto
{
    public int Id { get; set; }
    public int SortOrder { get; set; }
}
