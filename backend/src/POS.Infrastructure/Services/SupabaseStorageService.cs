using System.Net.Http.Headers;
using POS.Application.Abstractions;

namespace POS.Infrastructure.Services;

public class SupabaseStorageService(SupabaseStorageOptions options, IHttpClientFactory clients) : IFileStorageService
{
    public async Task<string> UploadAsync(Stream content, string contentType, string path, CancellationToken cancellationToken = default)
    {
        var client = clients.CreateClient();
        client.DefaultRequestHeaders.Add("apikey", options.SecretKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.SecretKey);

        using var body = new StreamContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var response = await client.PostAsync($"{options.Url}/storage/v1/object/{options.Bucket}/{path}", body, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Supabase Storage upload failed ({(int)response.StatusCode}): {error}");
        }

        return $"/api/uploads/file/{path}";
    }

    public async Task<StoredFile> DownloadAsync(string path, CancellationToken cancellationToken = default)
    {
        var client = clients.CreateClient();
        client.DefaultRequestHeaders.Add("apikey", options.SecretKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.SecretKey);

        using var response = await client.GetAsync(
            $"{options.Url}/storage/v1/object/authenticated/{options.Bucket}/{path}", cancellationToken);
        if (!response.IsSuccessStatusCode) throw new FileNotFoundException("Stored image was not found.", path);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return new StoredFile(await response.Content.ReadAsByteArrayAsync(cancellationToken), contentType);
    }
}
