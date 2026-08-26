using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Application.AI;
public record AiSettingsDto(Guid? Id, string Provider, string Model, string? BaseUrl, string? ApiKeyLast4, bool IsActive);
public record UpdateAiSettingsRequest(string Provider, string Model, string? BaseUrl, string? ApiKey, bool IsActive);
public record GenerateInsightRequest(string RequestType, DateOnly From, DateOnly To, Guid? BranchId, string? Question = null);
public record AiInsightDto(Guid Id, string RequestType, string Result, DateTime CreatedAt);
public record AiTestResultDto(string Reply);

public class AiInsightService(IAppDbContext db, ICurrentUserService currentUser, IDataProtectionProvider protection, IHttpClientFactory clients)
{
    private readonly IDataProtector protector = protection.CreateProtector("POS.AI.ApiKey.v1");
    public async Task<AiSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var s = await db.AiProviderSettings.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive, ct);
        if (s is null) return new(null, "OpenAI", "gpt-4.1-mini", null, null, false);
        var key = protector.Unprotect(s.ApiKeyEncrypted); return new(s.Id, s.Provider, s.Model, s.BaseUrl, key.Length <= 4 ? key : key[^4..], s.IsActive);
    }
    public async Task<AiSettingsDto> SaveSettingsAsync(UpdateAiSettingsRequest r, CancellationToken ct = default)
    {
        var provider = r.Provider.Trim();
        var model = r.Model.Trim();
        if (provider is not ("OpenAI" or "Anthropic" or "Custom")) throw new ValidationException("Supported AI providers are OpenAI, Anthropic, or Custom.");
        if (string.IsNullOrWhiteSpace(model) || model.Length > 100) throw new ValidationException("A valid AI model name is required.");
        string? baseUrl = null;
        if (provider == "Custom")
        {
            baseUrl = r.BaseUrl?.Trim();
            if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                throw new ValidationException("A valid API base URL (http/https) is required for a custom provider.");
        }
        var all = await db.AiProviderSettings.ToListAsync(ct); foreach (var item in all) item.IsActive = false;
        var current = all.FirstOrDefault(x => x.Provider == provider && x.Model == model) ?? new AiProviderSetting { Id = Guid.NewGuid() };
        if (!all.Contains(current)) db.AiProviderSettings.Add(current);
        current.Provider = provider; current.Model = model; current.BaseUrl = baseUrl; current.IsActive = r.IsActive;
        if (!string.IsNullOrWhiteSpace(r.ApiKey)) current.ApiKeyEncrypted = protector.Protect(r.ApiKey);
        if (string.IsNullOrWhiteSpace(current.ApiKeyEncrypted)) throw new ValidationException("API key is required.");
        await db.SaveChangesAsync(ct); return await GetSettingsAsync(ct);
    }
    public async Task<AiInsightDto> GenerateAsync(GenerateInsightRequest r, CancellationToken ct = default)
    {
        var isCustom = r.RequestType.Equals("Custom", StringComparison.OrdinalIgnoreCase);
        var question = r.Question?.Trim();
        if (isCustom && string.IsNullOrWhiteSpace(question)) throw new ValidationException("Write a question to analyze.");
        var branchId = currentUser.BypassBranchFilter ? r.BranchId : currentUser.BranchId;
        var sales = await db.Sales.AsNoTracking().Where(s => s.BusinessDate >= r.From && s.BusinessDate <= r.To && s.Status == SaleStatus.Completed && (!branchId.HasValue || s.BranchId == branchId)).Select(s => new { s.BusinessDate, s.TotalAmount, s.DiscountAmount }).ToListAsync(ct);
        var shifts = await db.Shifts.AsNoTracking().Where(s => !branchId.HasValue || s.BranchId == branchId).OrderByDescending(s => s.OpenedAt).Take(30).Select(s => new { s.VarianceAmount, s.OpenedAt }).ToListAsync(ct);
        var settings = await db.AiProviderSettings.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive, ct) ?? throw new ValidationException("Configure an AI provider first.");
        var instruction = isCustom ? question! : $"a concise {r.RequestType} insight";
        var prompt = $"You are a POS analytics assistant. Respond in Arabic only. Given this data: sales={JsonSerializer.Serialize(sales)}, shifts={JsonSerializer.Serialize(shifts)} — {instruction}. Clearly label any forecasts as estimates based on sales history.";
        var result = await CallProviderAsync(settings, prompt, 1200, ct);
        var label = isCustom ? question![..Math.Min(question.Length, 150)] : r.RequestType;
        var audit = new AiInsightRequest { Id = Guid.NewGuid(), RequestedByUserId = currentUser.UserId!.Value, BranchId = branchId, RequestType = label, CreatedAt = DateTime.UtcNow, ResultSummary = result[..Math.Min(result.Length, 8000)] };
        db.AiInsightRequests.Add(audit); await db.SaveChangesAsync(ct); return new(audit.Id, audit.RequestType, result, audit.CreatedAt);
    }

    public async Task<IReadOnlyList<AiInsightDto>> ListRecentAsync(int take, CancellationToken ct = default)
    {
        var branchId = currentUser.BypassBranchFilter ? null : currentUser.BranchId;
        return await db.AiInsightRequests.AsNoTracking()
            .Where(x => !branchId.HasValue || x.BranchId == branchId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(take, 1, 50))
            .Select(x => new AiInsightDto(x.Id, x.RequestType, x.ResultSummary, x.CreatedAt))
            .ToListAsync(ct);
    }

    // Lets the settings page verify a saved provider actually works, without spending a full
    // insight prompt or writing to the insight history.
    public async Task<AiTestResultDto> TestConnectionAsync(CancellationToken ct = default)
    {
        var settings = await db.AiProviderSettings.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive, ct)
            ?? throw new ValidationException("Save an AI provider configuration first.");
        var reply = await CallProviderAsync(settings, "Reply with exactly one word: OK", 16, ct);
        return new AiTestResultDto(reply.Trim());
    }

    private async Task<string> CallProviderAsync(AiProviderSetting settings, string prompt, int maxTokens, CancellationToken ct)
    {
        var client = clients.CreateClient(); var key = protector.Unprotect(settings.ApiKeyEncrypted); HttpResponseMessage response;
        if (settings.Provider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase))
        {
            client.DefaultRequestHeaders.Add("x-api-key", key); client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            response = await client.PostAsJsonAsync("https://api.anthropic.com/v1/messages", new { model = settings.Model, max_tokens = maxTokens, messages = new[] { new { role = "user", content = prompt } } }, ct);
        }
        else
        {
            var endpoint = settings.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
                ? "https://api.openai.com/v1/chat/completions"
                : settings.BaseUrl ?? throw new ValidationException("Configure an AI provider first.");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            response = await client.PostAsJsonAsync(endpoint, new { model = settings.Model, max_tokens = maxTokens, messages = new[] { new { role = "user", content = prompt } } }, ct);
        }
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new ValidationException($"AI provider request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {Truncate(body, 300)}");
        }
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return settings.Provider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase)
            ? json.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty
            : json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength] + "...";
}
