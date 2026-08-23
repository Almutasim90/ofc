namespace POS.Application.Auth;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string Token,
    Guid UserId,
    string FullName,
    Guid? BranchId,
    string RoleName,
    string PreferredLanguage,
    string? PreferredTheme,
    IReadOnlyCollection<string> Permissions);
