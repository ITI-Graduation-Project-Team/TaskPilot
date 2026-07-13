using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [Route("api/notifications")]
    [Authorize]
    public class NotificationController : ApiControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;

        public NotificationController(INotificationService notificationService, ICurrentUserService currentUserService)
        {
            _notificationService = notificationService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult> GetNotifications([FromQuery] bool? isUnread = null)
        {
            var userId = _currentUserService.UserId;

            if (userId == null || userId == Guid.Empty)
            {
                return Unauthorized();
            }

            var result = await _notificationService.GetUserNotificationsAsync(userId.Value, isUnread);

            return HandleResult(result, SuccessCodes.Notification.Retrieved);
        }

        [HttpPatch("{id:guid}/read")]
        public async Task<ActionResult> MarkAsRead(Guid id)
        {
            var userId = _currentUserService.UserId;

            if (userId == null || userId == Guid.Empty)
            {
                return Unauthorized();
            }

            var result = await _notificationService.MarkAsReadAsync(userId.Value, id);

            return HandleResult(result, SuccessCodes.Notification.MarkedAsRead);
        }

        [HttpPost("read-all")]
        public async Task<ActionResult> MarkAllAsRead()
        {
            var userId = _currentUserService.UserId;

            if (userId == null || userId == Guid.Empty)
            {
                return Unauthorized();
            }

            var result = await _notificationService.MarkAllAsReadAsync(userId.Value);

            return HandleResult(result, SuccessCodes.Notification.MarkedAsRead);
        }
    }
}
