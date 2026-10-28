using System.ComponentModel.DataAnnotations;

namespace BeRdp.Backend.Dtos.Requests;

public class EntitledAdGroupRequest
{
    [Required]
    [MaxLength(256)]
    public string GroupName { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? DomainName { get; set; }
}
