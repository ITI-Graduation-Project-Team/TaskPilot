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
        public bool IsProfileCompleted { get; set; } = false;
        public DateTime? LastCvProcessedAt { get; set; }
        public AiProcessingStatus CvProcessingStatus { get; set; }
        public string? LatestCvUrl { get; set; }
        public string? CvPublicId { get; set; }
        public long CvFileSize { get; set; } = 0;

        public bool IsDeactivated { get; set; }
        public DateTime? DeactivatedAt { get; set; }
        public Guid? DeactivatedBy { get; set; }
        public string? DeactivationReason { get; set; }

        public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
        public ICollection<ProjectEmployee> ProjectEmployees { get; set; } = new List<ProjectEmployee>();
    }
}
