using System;
using System.Collections.Generic;

namespace TaskPilot.AI.Models.Sprint
{
    public class SprintRiskContext
    {
        public string SprintGoal { get; set; } = string.Empty;
        public int DaysRemaining { get; set; }
        public int TotalWorkingDaysInSprint { get; set; }
        public List<TaskRiskSnapshot> Tasks { get; set; } = new();
        public List<TeamMemberSnapshot> TeamMembers { get; set; } = new();
    }

    public class TaskRiskSnapshot
    {
        public Guid TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "ToDo" | "InProgress" | "Review" | "Done"
        public decimal EstimatedHours { get; set; }
        public decimal ActualHours { get; set; }
        public bool IsBlocked { get; set; }
        public List<string> RequiredSkills { get; set; } = new();
        public string? AssignedEmployeeName { get; set; }
        public Guid? AssignedEmployeeId { get; set; }
    }

    public class TeamMemberSnapshot
    {
        public Guid EmployeeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal ScheduledHoursToday { get; set; } // from CalenderEvent
        public decimal MaxSprintHours { get; set; }
        public List<string> Skills { get; set; } = new();
    }
}
