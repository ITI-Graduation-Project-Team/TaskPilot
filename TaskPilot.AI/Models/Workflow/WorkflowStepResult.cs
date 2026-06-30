namespace TaskPilot.AI.Models.Workflow
{
    public class WorkflowStepResult
    {
        public bool Success
        {
            get;
            set;
        }

        public string CurrentStage
        {
            get;
            set;
        }
        =
            string.Empty;

        public bool ReadyForNextStage
        {
            get;
            set;
        }

        public List<string>
            ActionsExecuted
        {
            get;
            set;
        }
        =
            new();

        public TimeSpan Duration
        {
            get;
            set;
        }
    }
}
