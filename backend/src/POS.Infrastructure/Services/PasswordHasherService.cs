using Microsoft.AspNetCore.Identity;
using POS.Application.Abstractions;
using POS.Domain.Entities;

namespace POS.Infrastructure.Services;

/// <summary>Thin wrapper over ASP.NET Core Identity's PasswordHasher for our custom User entity.</summary>
public class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(null!, hash, password) != PasswordVerificationResult.Failed;
}
