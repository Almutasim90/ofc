namespace POS.Infrastructure.Services;

public record JwtOptions(string Secret, string Issuer, string Audience, int ExpiryMinutes);
