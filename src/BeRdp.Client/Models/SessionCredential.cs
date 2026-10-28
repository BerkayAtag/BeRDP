namespace BeRdp.Client.Models;

public sealed class SessionCredential
{
    public string InputUsername { get; init; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseWindowsSso { get; init; }
}
