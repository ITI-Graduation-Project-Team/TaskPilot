using TaskPilot.DTOs.Assignment;

namespace TaskPilot.Services.Assignment;

public class VelocityScoreCalculator : IScoreCalculator
{
    public double Calculate(TaskSnapshotDto task, DeveloperSnapshotDto developer)
    {
        if (!developer.HasHistoricalData)
            return 50;

        var deliveryRatio = developer.HistoricalVelocity.GetValueOrDefault(1);
        var reliabilityScore = 100 - Math.Abs(1 - deliveryRatio) * 100;
        return Math.Clamp(reliabilityScore, 0, 100);
    }
}
