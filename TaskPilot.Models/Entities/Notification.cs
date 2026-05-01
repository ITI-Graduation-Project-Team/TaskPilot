using System.ComponentModel.DataAnnotations.Schema;
using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{
        public class Notification : AuditableEntity<Guid>
        {
            public Guid UserId { get; set; }
            public User User { get; set; } = null!;

            public NotificationType Type { get; set; }

           public string MessageEn { get; set; } = string.Empty;
             public string MessageAr { get; set; } = string.Empty;
        public string? Url { get; set; }
        public bool IsRead { get; set; } = false;
        }

}
