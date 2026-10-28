namespace BeRdp.Backend.Services;

public class ActiveDirectoryConfiguration
{
    public bool Enabled { get; set; }
    public string DomainName { get; set; } = string.Empty;
    public string? LdapContainer { get; set; }
    public bool UseMachineCredentials { get; set; } = true;
    public string? BindUsername { get; set; }
    public string? BindPassword { get; set; }
}
