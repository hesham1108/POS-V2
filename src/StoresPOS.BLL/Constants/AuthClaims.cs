namespace StoresPOS.BLL.Constants;

/// <summary>Extra JWT claims (Phase 21).</summary>
public static class AuthClaims
{
    public const string PermissionStamp = "pstamp";
    public const string SecurityStamp = "sstamp";
    public const string MustChangePassword = "pwchange";

    /// <summary>Wrong passwords in a row before the account locks.</summary>
    public const int MaxFailedLogins = 5;

    /// <summary>How long a locked account stays locked.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
}
