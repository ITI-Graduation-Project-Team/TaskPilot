namespace TaskPilot.AI.Models.Planning
{
    public class RiskAssessment
    {
        public List<string>
            HighRisks
        { get; set; }
            = new();

        public List<string>
            MediumRisks
        { get; set; }
            = new();

        public List<string>
            LowRisks
        { get; set; }
            = new();
    }
}