using TaskPilot.DTOs.Assignment;

namespace TaskPilot.Services.Assignment;

public class AvailabilityScoreCalculator : IScoreCalculator
{
    public double Calculate(TaskSnapshotDto task, DeveloperSnapshotDto developer)
    {
        if (developer.RemainingHours <= 0)
            return 0;

        var requiredHours = (double)task.EstimatedHours;
        if (requiredHours <= 0)
            return 100;

        var score = (developer.RemainingHours / requiredHours) * 100;
        
        return score > 100 ? 100 : score;
    }
}
