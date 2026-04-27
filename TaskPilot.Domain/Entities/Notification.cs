using System.ComponentModel.DataAnnotations.Schema;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Domain.Entities
{
    public class Notification
    {
        [ForeignKey("User")]
        public Guid UserId { get; set; }
        public User User { get; set; }
        public NotificationType Type { get; set; }///
        public string Message { get; set; }
        public string Url { get; set; }
        public bool IsRead { get; set; }


    }
}
