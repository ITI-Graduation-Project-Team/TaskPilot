using TaskPilot.DTOs.Assignment;

namespace TaskPilot.Services.Assignment;

public interface IScoreCalculator
{
    double Calculate(TaskSnapshotDto task, DeveloperSnapshotDto developer);
}
