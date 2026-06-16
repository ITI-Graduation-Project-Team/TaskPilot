namespace TaskPilot.AI.Models.Requirements
{
    public class CompletenessReport
    {
        public float Score
        {
            get;
            set;
        }

        public bool ReadyForPlanning
        {
            get;
            set;
        }

        public List<string>
            CriticalMissingAreas
        {
            get;
            set;
        }
        =
            new();

        public List<string>
            OptionalMissingAreas
        {
            get;
            set;
        }
        =
            new();

        public List<string>
            WeakRequirements
        {
            get;
            set;
        }
        =
            new();
    }
}
