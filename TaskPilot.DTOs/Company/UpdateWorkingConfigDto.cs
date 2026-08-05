namespace TaskPilot.DTOs.Company
{
    public class UpdateWorkingConfigDto
    {
        public decimal WorkingHoursPerDay { get; set; } = 8.0m;
        public int WorkingDaysMask { get; set; } = 62;
        public decimal DefaultCapacityBufferPercentage { get; set; } = 0.80m;
    }
}
