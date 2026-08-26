namespace POS.Domain.Entities;
public class AiProviderSetting { public Guid Id { get; set; } public string Provider { get; set; } = string.Empty; public string Model { get; set; } = string.Empty; public string? BaseUrl { get; set; } public string ApiKeyEncrypted { get; set; } = string.Empty; public bool IsActive { get; set; } }
