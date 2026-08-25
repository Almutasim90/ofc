namespace POS.Infrastructure.Services;

public record SupabaseStorageOptions(string Url, string SecretKey, string Bucket = "uploads");
