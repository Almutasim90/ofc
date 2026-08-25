using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Abstractions;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController, Route("api/uploads")]
public class UploadsController(IFileStorageService storage) : ControllerBase
{
    private static readonly HashSet<string> AllowedContentTypes =
    [
        "image/jpeg", "image/png", "image/webp", "image/svg+xml"
    ];

    [HttpPost("channel-logo"), RequirePermission(PermissionKeys.ChannelsManage)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<object>> UploadChannelLogo(IFormFile file, CancellationToken ct)
        => await SaveImage(file, "channels", "Logo", ct, 5 * 1024 * 1024);

    [HttpPost("product-image"), RequirePermission(PermissionKeys.ProductsManage)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<object>> UploadProductImage(IFormFile file, CancellationToken ct)
        => await SaveImage(file, "products", "Image", ct, 5 * 1024 * 1024);

    [HttpGet("file/{**path}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetFile(string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains("..", StringComparison.Ordinal)
            || !(path.StartsWith("channels/", StringComparison.Ordinal) || path.StartsWith("products/", StringComparison.Ordinal)))
            return NotFound();
        try
        {
            var file = await storage.DownloadAsync(path, ct);
            return File(file.Content, file.ContentType);
        }
        catch (FileNotFoundException) { return NotFound(); }
    }

    private async Task<ActionResult<object>> SaveImage(
        IFormFile file,
        string subfolder,
        string label,
        CancellationToken ct,
        long maxBytes = 2 * 1024 * 1024)
    {
        if (file.Length == 0 || file.Length > maxBytes)
            return BadRequest(new { error = $"{label} must be between 1 byte and {maxBytes / 1024 / 1024} MB." });

        if (!AllowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            return BadRequest(new { error = "Only JPG, PNG, WebP, and SVG images are supported." });

        var extension = file.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            _ => throw new InvalidOperationException()
        };

        var fileName = $"{Guid.NewGuid():N}{extension}";
        await using var stream = file.OpenReadStream();
        string url;
        try
        {
            url = await storage.UploadAsync(stream, file.ContentType, $"{subfolder}/{fileName}", ct);
        }
        catch (InvalidOperationException ex)
        {
            // Storage failures are actionable configuration/upstream errors,
            // not unknown application crashes. The storage response contains
            // no credentials and helps administrators fix Dokploy variables.
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }

        return Ok(new { url });
    }
}
