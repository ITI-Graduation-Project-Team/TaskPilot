using TaskPilot.Domain.Enums;

namespace TaskPilot.Domain.Entities
{
    public class Developer : User
    {
        public float? HistoricalVelocity { get; set; }
        public Availability? AvailabilityStatus { get; set; }
        public float? MaxSprintHours { get; set; }
        public ICollection<Task> AssignedTasks { get; set; } = new List<Task>();
        public ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();


    }
}
