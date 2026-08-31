namespace POS.Application.Abstractions;

public interface IRawPrinterClient
{
    Task SendAsync(string ipAddress, int port, byte[] payload, CancellationToken cancellationToken = default);
}
