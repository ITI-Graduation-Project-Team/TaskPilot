namespace TaskPilot.DTOs.Planning
{
    public class SprintSelectionOptions
    {
        public decimal TargetSprintHours { get; set; }
        public decimal MinUtilizationPercent { get; set; } = 0.85m;
        public decimal MaxUtilizationPercent { get; set; } = 1.00m;
    }
}
