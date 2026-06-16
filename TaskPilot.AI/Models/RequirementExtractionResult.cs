namespace TaskPilot.AI.Models
{
    public class RequirementExtractionResult
    {
        public List<string>
            BusinessRequirements
        { get; set; }
            = new();

        public List<string>
            TechnicalRequirements
        { get; set; }
            = new();

        public List<string>
            Constraints
        { get; set; }
            = new();

        public List<string>
            Integrations
        { get; set; }
            = new();

        public List<string>
            ScaleRequirements
        { get; set; }
            = new();
    }
}