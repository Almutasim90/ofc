using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace POS.Application.QrOrdering;

public sealed class QrTokenService
{
    private readonly byte[] secret;

    public QrTokenService(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException("QR signing secret must contain at least 32 bytes.");
        this.secret = Encoding.UTF8.GetBytes(secret);
    }

    public string Generate(Guid pointId, int version)
    {
        var payload = Payload(pointId, version);
        return $"v1.{version}.{WebEncoders.Base64UrlEncode(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(payload)))}";
    }

    public bool Verify(Guid pointId, int expectedVersion, string token)
    {
        var parts = token.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || parts[0] != "v1" || !int.TryParse(parts[1], out var version) || version != expectedVersion) return false;
        byte[] supplied;
        try { supplied = WebEncoders.Base64UrlDecode(parts[2]); }
        catch (FormatException) { return false; }
        var expected = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(Payload(pointId, version)));
        return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    private static string Payload(Guid pointId, int version) => $"qr-point:v1:{pointId:N}:{version}";
}
