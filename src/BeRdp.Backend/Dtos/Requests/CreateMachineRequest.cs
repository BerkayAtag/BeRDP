using System.ComponentModel.DataAnnotations;

namespace BeRdp.Backend.Dtos.Requests;

public class CreateMachineRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(45)]
    public string IpAddress { get; set; } = string.Empty;
    [MaxLength(253)]
    public string? RdpHostName { get; set; }

    [Range(1, 65535)]
    public int RdpPort { get; set; } = 3389;

    public List<Guid> EntitledUserIds { get; set; } = [];
    public List<EntitledAdGroupRequest> EntitledAdGroups { get; set; } = [];
}
