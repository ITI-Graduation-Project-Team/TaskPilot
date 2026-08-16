using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.SubscriptionPlans
{
    public class SubscriptionPlanDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal MonthlyPrice { get; set; }
        public decimal AnnualPrice { get; set; }
        public string Currency { get; set; } = "EGP";
        public int MaxProjects { get; set; }
        public int MaxUsersPerProject { get; set; }
        public int MaxStorageMb { get; set; }
        public int MaxTokensPerMonth { get; set; }
        public bool HasAi { get; set; }
        public bool HasAdvancedAnalytics { get; set; }
        public bool HasTrial { get; set; }
        public int TrialDays { get; set; }
    }
}
