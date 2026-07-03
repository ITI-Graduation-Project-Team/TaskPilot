using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Assignment;

public class TaskSnapshotDto
{
    public Guid TaskId { get; set; }
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public decimal EstimatedHours { get; set; }
    public TaskPriority Priority { get; set; }
    public EffortSize EffortSize { get; set; }
    public TaskType Type { get; set; }
    public List<TaskRequiredSkillDto> RequiredSkills { get; set; } = new();
}
