namespace BeRdp.Backend.Models;

public class MachineAdGroupEntitlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MachineId { get; set; }
    public Machine Machine { get; set; } = null!;
    public string GroupName { get; set; } = string.Empty;
    public string? DomainName { get; set; }
}
