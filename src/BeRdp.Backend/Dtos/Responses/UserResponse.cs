using BeRdp.Backend.Models;

namespace BeRdp.Backend.Dtos.Responses;

public class UserResponse
{
    public Guid Id { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public bool IsAdmin { get; init; }
    public bool IsActiveDirectoryUser { get; init; }
    public string? DomainName { get; init; }
    public string? UserPrincipalName { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }
    public bool CanAccessAdminConsole { get; init; }
    public ConsoleRole ConsoleRole { get; init; }
}
