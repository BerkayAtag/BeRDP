namespace BeRdp.Backend.Dtos.Requests;

public class UpdateUserRoleRequest
{
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Optional explicit console grant. When omitted, promoting keeps/grants console
    /// access and demoting keeps any existing console access as read-only.
    /// </summary>
    public bool? CanAccessAdminConsole { get; set; }
}
