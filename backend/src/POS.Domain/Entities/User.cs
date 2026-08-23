namespace POS.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Null means the user is not tied to a single branch (e.g. General Manager).</summary>
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public string PreferredLanguage { get; set; } = "ar";

    /// <summary>Null means the user hasn't picked one explicitly - the client falls back to OS preference.</summary>
    public string? PreferredTheme { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<UserPermissionOverride> PermissionOverrides { get; set; } = new List<UserPermissionOverride>();
}
