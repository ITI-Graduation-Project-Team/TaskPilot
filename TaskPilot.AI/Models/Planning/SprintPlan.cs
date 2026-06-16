namespace TaskPilot.AI.Models.Planning
{
    public class SprintPlan
    {
        public int TotalSprints
        { get; set; }

        public int SprintDurationWeeks
        { get; set; }

        public List<string>
            SprintGoals
        { get; set; }
            = new();
    }
}