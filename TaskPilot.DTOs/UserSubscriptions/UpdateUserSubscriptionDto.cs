using System;
using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.UserSubscriptions
{
    public class UpdateUserSubscriptionDto
    {
        [Required]
        public string Status { get; set; } = string.Empty;
        public bool AutoRenew { get; set; }
        public DateTime EndDate { get; set; }
    }
}
