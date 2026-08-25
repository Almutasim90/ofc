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

        return $"{options.Url}/storage/v1/object/public/{options.Bucket}/{path}";
    }
}
