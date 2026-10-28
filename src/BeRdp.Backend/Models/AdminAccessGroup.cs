namespace BeRdp.Backend.Models;

public class AdminAccessGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string GroupName { get; set; } = string.Empty;
    public string? DomainName { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
