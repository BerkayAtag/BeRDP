namespace BeRdp.Backend.Models;

public class UserMachine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid MachineId { get; set; }
    public Machine Machine { get; set; } = null!;
}
