namespace TaskPilot.AI.Models.Workflow
{
    public class AgentDecision
    {
        public string AgentName
        {
            get;
            set;
        }
        =
            string.Empty;

        public string Decision
        {
            get;
            set;
        }
        =
            string.Empty;

        public DateTime Timestamp
        {
            get;
            set;
        }
        =
            DateTime.UtcNow;
    }
}
