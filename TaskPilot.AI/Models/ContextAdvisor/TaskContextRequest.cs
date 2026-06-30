namespace TaskPilot.AI.Models.ContextAdvisor
{
    public class TaskContextRequest
    {
        public Guid? ProjectId { get; set; }

        public Guid? TaskId { get; set; }

        public string TaskTitle { get; set; } = string.Empty;

        public string? TaskDescription { get; set; }

        public string? AcceptanceCriteria { get; set; }

        public string? TechnicalSummary { get; set; }

        public List<string> RelatedPastTasks { get; set; } = new();

        public int TopK { get; set; } = 6;
    }
}
