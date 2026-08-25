namespace POS.Application.Abstractions;

public interface IFileStorageService
{
    Task<string> UploadAsync(Stream content, string contentType, string path, CancellationToken cancellationToken = default);
    Task<StoredFile> DownloadAsync(string path, CancellationToken cancellationToken = default);
}

public record StoredFile(byte[] Content, string ContentType);
