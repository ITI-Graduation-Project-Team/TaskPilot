using TaskPilot.DTOs.Assignment;

namespace TaskPilot.Services.Assignment;

public class VelocityScoreCalculator : IScoreCalculator
{
    public double Calculate(TaskSnapshotDto task, DeveloperSnapshotDto developer)
    {
        if (!developer.HasHistoricalData)
            return 50;

        var velocity = developer.HistoricalVelocity.GetValueOrDefault(0);
        if (velocity > 100) return 100;
        if (velocity < 0) return 0;
        return velocity;
    }
}
