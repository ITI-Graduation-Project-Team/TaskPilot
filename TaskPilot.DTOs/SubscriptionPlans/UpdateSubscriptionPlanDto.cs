using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.SubscriptionPlans
{
    public class UpdateSubscriptionPlanDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [Range(0, double.MaxValue)]
        public decimal MonthlyPrice { get; set; }
        
        [Range(0, double.MaxValue)]
        public decimal AnnualPrice { get; set; }
        
        [Required]
        [MaxLength(10)]
        public string Currency { get; set; } = "EGP";
        
        [Range(1, int.MaxValue)]
        public int MaxProjects { get; set; }
        
        [Range(1, int.MaxValue)]
        public int MaxUsersPerProject { get; set; }
        
        public bool HasAi { get; set; }
        
        public bool HasAdvancedAnalytics { get; set; }
        
        public bool HasTrial { get; set; }
        
        public int? TrialDays { get; set; }
    }
}
