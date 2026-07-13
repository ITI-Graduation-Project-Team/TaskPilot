using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace TaskPilot.Presentation.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
    }
}
