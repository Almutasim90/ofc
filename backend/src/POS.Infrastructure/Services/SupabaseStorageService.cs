using System.Net.Http.Headers;
using POS.Application.Abstractions;

namespace POS.Infrastructure.Services;

public class SupabaseStorageService(SupabaseStorageOptions options, IHttpClientFactory clients) : IFileStorageService
{
    public async Task<string> UploadAsync(Stream content, string contentType, string path, CancellationToken cancellationToken = default)
    {
        var client = clients.CreateClient();
        try
        {
            client.DefaultRequestHeaders.Add("apikey", options.SecretKey);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.SecretKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "SUPABASE_SECRET_KEY has an invalid format in Dokploy. Remove quotes, spaces, and line breaks.", ex);
        }

        using var body = new StreamContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync($"{options.Url}/storage/v1/object/{options.Bucket}/{path}", body, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or FormatException)
        {
            throw new InvalidOperationException(
                "Supabase Storage could not be reached. Verify SUPABASE_URL and SUPABASE_SECRET_KEY in Dokploy.", ex);
        }
        using (response)
        {
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Supabase Storage upload failed ({(int)response.StatusCode}): {error}");
        }
        }

        return $"{options.Url}/storage/v1/object/public/{options.Bucket}/{path}";
    }

    public async Task<StoredFile> DownloadAsync(string path, CancellationToken cancellationToken = default)
    {
        // The uploads bucket is public. Read it through the public endpoint so
        // display does not depend on production secret-key formatting, while
        // retaining this method for rows saved with the temporary /api proxy URL.
        var client = clients.CreateClient();
        using var response = await client.GetAsync(
            $"{options.Url}/storage/v1/object/public/{options.Bucket}/{path}", cancellationToken);
        if (!response.IsSuccessStatusCode) throw new FileNotFoundException("Stored image was not found.", path);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return new StoredFile(await response.Content.ReadAsByteArrayAsync(cancellationToken), contentType);
    }
}
