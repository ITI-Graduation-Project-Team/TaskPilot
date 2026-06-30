namespace TaskPilot.AI.Models.Requirements
{
    public class StructuredRequirements
    {
        public string ProjectSummary
        {
            get;
            set;
        }
        =
            string.Empty;

        public List<string>
            BusinessGoals
        {
            get;
            set;
        }
        =
            new();

        public List<string>
            CoreFeatures
        {
            get;
            set;
        }
        =
            new();

        public List<string>
            Constraints
        {
            get;
            set;
        }
        =
            new();

        public List<string>
            Integrations
        {
            get;
            set;
        }
        =
            new();

        public List<string>
            ScaleRequirements
        {
            get;
            set;
        }
        =
            new();

        public List<string>
            Risks
        {
            get;
            set;
        }
        =
            new();
    }
}
