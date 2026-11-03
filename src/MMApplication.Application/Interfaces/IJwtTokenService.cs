using MMApplication.Domain.Entities;

namespace MMApplication.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(Customer customer, out DateTime expiresAt);
    string GenerateToken(SystemUser user, out DateTime expiresAt);
}
