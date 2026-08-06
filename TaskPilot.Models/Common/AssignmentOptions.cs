namespace TaskPilot.Models.Common;

public class AssignmentOptions
{
    public const string SectionName = "Assignment";

    public double HighUtilizationThreshold { get; set; }

    public int RecommendedTasksPerDeveloper { get; set; }

    public int MaxExplanationConcurrency { get; set; } = 5;
}
