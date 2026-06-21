namespace TaskPilot.AI.Models.Planning
{
    public class GeneratedWbs
    {
        public List<GeneratedSprint> Sprints { get; set; } = new();
    }

    public class GeneratedSprint
    {
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string? SprintGoalEn { get; set; }
        public string? SprintGoalAr { get; set; }
        public List<GeneratedUserStory> UserStories { get; set; } = new();
    }

    public class GeneratedUserStory
    {
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public string? AcceptanceCriteriaEn { get; set; }
        public string? AcceptanceCriteriaAr { get; set; }
        public List<GeneratedTask> Tasks { get; set; } = new();
    }

    public class GeneratedTask
    {
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public string EffortSize { get; set; } = string.Empty; // "Small" | "Medium" | "Large"
        public string Type { get; set; } = string.Empty;       // "Technical" | "NonTechnical"
        public decimal EstimatedHours { get; set; }
    }
}
