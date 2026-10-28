using System.Text.Json.Serialization;

namespace BeRdp.Client.Models;

public class MachineDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("ipAddress")]
    public string IpAddress { get; init; } = string.Empty;

    [JsonPropertyName("rdpHostName")]
    public string? RdpHostName { get; init; }

    [JsonPropertyName("rdpPort")]
    public int RdpPort { get; init; }

    [JsonPropertyName("isOnline")]
    public bool IsOnline { get; init; }
}
