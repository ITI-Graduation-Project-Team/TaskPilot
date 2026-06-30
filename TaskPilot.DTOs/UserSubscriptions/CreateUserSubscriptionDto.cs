using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.UserSubscriptions
{
    public class CreateUserSubscriptionDto
    {
        [Required]
        public int SubscriptionPlanId { get; set; }
        
        [Required]
        [RegularExpression("Monthly|Annually", ErrorMessage = "BillingCycle must be Monthly or Annually.")]
        public string BillingCycle { get; set; } = "Monthly";

        public bool AutoRenew { get; set; } = true;
    }
}
