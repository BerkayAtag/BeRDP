namespace BeRdp.Backend.Dtos.Auth;

public class LoginResponse
{
    public required Guid UserId { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsAdmin { get; init; }
    public bool IsActiveDirectoryUser { get; init; }
    public string RdpUsername { get; init; } = string.Empty;
    public required string Token { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }

    // Admin Console Access Control
    public bool CanAccessAdminConsole { get; init; }
    public Models.ConsoleRole ConsoleRole { get; init; } = Models.ConsoleRole.Standard;
}
