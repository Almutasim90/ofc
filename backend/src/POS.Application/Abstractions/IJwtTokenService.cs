using POS.Domain.Entities;

namespace POS.Application.Abstractions;

public interface IJwtTokenService
{
    string GenerateToken(User user, IReadOnlyCollection<string> permissions);
}
