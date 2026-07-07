using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Enums;

namespace TaskPilot.Services.Assignment;

public class ExperienceScoreCalculator : IScoreCalculator
{
    public double Calculate(TaskSnapshotDto task, DeveloperSnapshotDto developer)
    {
        return developer.SeniorityLevel switch
        {
            SeniorityLevel.Junior => 25,
            SeniorityLevel.MidLevel => 60,
            SeniorityLevel.Senior => 85,
            SeniorityLevel.Lead => 100,
            _ => 25
        };
    }
}
