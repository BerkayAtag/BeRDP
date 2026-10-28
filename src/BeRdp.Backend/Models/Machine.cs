namespace BeRdp.Backend.Models;

public class Machine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? RdpHostName { get; set; }
    public int RdpPort { get; set; } = 3389;
    public ICollection<UserMachine> UserMachines { get; set; } = new List<UserMachine>();
    public ICollection<MachineAdGroupEntitlement> AdGroupEntitlements { get; set; } = new List<MachineAdGroupEntitlement>();
}
