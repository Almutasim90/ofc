namespace POS.Application.Common;

public record RoleDto(Guid Id, string Name, string? Description);

public record PermissionDto(Guid Id, string Key, string? Description);
