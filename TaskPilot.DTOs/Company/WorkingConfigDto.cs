namespace TaskPilot.DTOs.Company
{
    public class WorkingConfigDto
    {
        public decimal WorkingHoursPerDay { get; set; }
        public int WorkingDaysMask { get; set; }
        public decimal DefaultCapacityBufferPercentage { get; set; }
    }
}
