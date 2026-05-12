using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{
    public class Employee : User
    {
        public string? JobTitle { get; set; }
        public SeniorityLevel? SeniorityLevel { get; set; }
        public int? TotalYearsOfExperience { get; set; }
        public decimal? HistoricalVelocity { get; set; }
        public decimal? MaxSprintHours { get; set; }
        public Availability? AvailabilityStatus { get; set; }

        public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
        public ICollection<ProjectEmployee> ProjectEmployees { get; set; } = new List<ProjectEmployee>();
    }
}
