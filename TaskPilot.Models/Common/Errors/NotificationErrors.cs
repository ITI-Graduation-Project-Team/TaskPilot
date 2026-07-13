using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskPilot.Models.Common.Errors
{
    public class NotificationErrors
    {
        public static Error NotFound() => new Error(
            "NOTIFICATION_NOT_FOUND",
            ErrorType.NotFound,
            "The requested notification was not found.");
    }
}
