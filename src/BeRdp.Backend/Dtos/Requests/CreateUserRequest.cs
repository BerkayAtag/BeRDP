using System.ComponentModel.DataAnnotations;

namespace BeRdp.Backend.Dtos.Requests;

public class CreateUserRequest
{
    [Required]
    [MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MinLength(14)]
    [MaxLength(72)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }
}
