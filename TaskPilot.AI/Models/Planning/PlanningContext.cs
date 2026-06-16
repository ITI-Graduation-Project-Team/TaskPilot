namespace TaskPilot.AI.Models.Planning
{
    public class PlanningContext
    {
        public TeamPlan?
            TeamPlan
        { get; set; }

        public SprintPlan?
            SprintPlan
        { get; set; }

        public RiskAssessment?
            Risks
        { get; set; }
    }
}