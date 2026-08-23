namespace POS.Domain.Entities;

public class UserPermissionOverride
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;

    /// <summary>true = grant in addition to role permissions, false = explicitly revoke.</summary>
    public bool IsGranted { get; set; }
}
