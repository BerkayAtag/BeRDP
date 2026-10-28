using System.ComponentModel.DataAnnotations;

namespace BeRdp.Backend.Dtos.Requests;

public sealed class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(14)]
    [MaxLength(72)]
    public string NewPassword { get; set; } = string.Empty;
}
