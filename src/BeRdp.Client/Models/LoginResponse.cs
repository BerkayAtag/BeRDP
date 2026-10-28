using System.Text.Json.Serialization;

namespace BeRdp.Client.Models;

public class LoginResponse
{
    [JsonPropertyName("userId")]
    public Guid UserId { get; init; }

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("isAdmin")]
    public bool IsAdmin { get; init; }

    public bool IsActiveDirectoryUser { get; init; }
    public string RdpUsername { get; init; } = string.Empty;

    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;

    [JsonPropertyName("lastLoginAtUtc")]
    public DateTime? LastLoginAtUtc { get; init; }
}
