using System.Net.Sockets;
using POS.Application.Abstractions;

namespace POS.Infrastructure.Services;

public sealed class TcpRawPrinterClient : IRawPrinterClient
{
    public async Task SendAsync(string ipAddress, int port, byte[] payload, CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(ipAddress, port, cancellationToken);
        await using var stream = client.GetStream();
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
