namespace POS.Application.Abstractions;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? BranchId { get; }
    string? RoleName { get; }
    IReadOnlyCollection<string> Permissions { get; }

    /// <summary>True when the current user should see data across all branches (General Manager or reports.global.view).</summary>
    bool BypassBranchFilter { get; }
}
