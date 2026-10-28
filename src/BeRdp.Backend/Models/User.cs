namespace BeRdp.Backend.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsActiveDirectoryUser { get; set; }
    public string? DomainName { get; set; }
    public string? UserPrincipalName { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public ICollection<UserMachine> UserMachines { get; set; } = new List<UserMachine>();

    // Admin Console Access Control
    public bool CanAccessAdminConsole { get; set; }
    public ConsoleRole ConsoleRole { get; set; } = ConsoleRole.Standard;
}

public enum ConsoleRole
{
    Standard = 0,  // Read-only access
    Admin = 1      // Full access
}
