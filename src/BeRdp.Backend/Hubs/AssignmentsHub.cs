using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BeRdp.Backend.Hubs;

[Authorize]
public class AssignmentsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userId, out _))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId!);
        }

        await base.OnConnectedAsync();
    }
}
