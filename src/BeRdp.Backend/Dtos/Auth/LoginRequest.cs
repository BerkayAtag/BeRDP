using System.ComponentModel.DataAnnotations;

namespace BeRdp.Backend.Dtos.Auth;

public class LoginRequest
{
    [Required]
    [MaxLength(256)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}
