namespace StoresPOS.BLL.Models;

public class StoreSettingsPersistenceOptions
{
    public const string SectionName = "StoreSettingsPersistence";

    /// <summary>Absolute path to App_Data/store-settings.json.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Absolute path to App_Data/license.json.</summary>
    public string LicenseFilePath { get; set; } = string.Empty;

    /// <summary>Absolute path to App_Data/print-templates.json (Phase 24).</summary>
    public string TemplatesFilePath { get; set; } = string.Empty;
}
