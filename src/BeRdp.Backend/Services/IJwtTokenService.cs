using BeRdp.Backend.Models;

namespace BeRdp.Backend.Services;

public interface IJwtTokenService
{
    string GenerateToken(User user, bool isAdmin, IReadOnlyList<string>? activeDirectoryGroupNames = null);
}
