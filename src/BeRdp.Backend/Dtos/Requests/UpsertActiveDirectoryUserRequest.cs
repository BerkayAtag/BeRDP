using System.ComponentModel.DataAnnotations;

namespace BeRdp.Backend.Dtos.Requests;

public class UpsertActiveDirectoryUserRequest
{
    [Required]
    [MaxLength(128)]
    public string SamAccountName { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(128)]
    public string DomainName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? UserPrincipalName { get; set; }

    public bool IsAdmin { get; set; }
}
