namespace POS.Application.Users;

public record UserDto(
    Guid Id,
    string FullName,
    string Username,
    Guid? BranchId,
    Guid RoleId,
    string RoleName,
    string PreferredLanguage,
    bool IsActive,
    DateTime CreatedAt);

public record CreateUserRequest(
    string FullName,
    string Username,
    string Password,
    Guid? BranchId,
    Guid RoleId,
    string PreferredLanguage);

public record UpdateUserRequest(
    string FullName,
    Guid? BranchId,
    Guid RoleId,
    string PreferredLanguage,
    bool IsActive);

public record PermissionOverrideDto(Guid PermissionId, string PermissionKey, bool? IsGranted);

public record SetPermissionOverrideRequest(Guid PermissionId, bool? IsGranted);
