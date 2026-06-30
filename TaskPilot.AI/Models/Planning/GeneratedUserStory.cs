namespace TaskPilot.AI.Models.Planning
{
    public class GeneratedUserStory
    {
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public string? AcceptanceCriteriaEn { get; set; }
        public string? AcceptanceCriteriaAr { get; set; }

        /// <summary>
        /// "Low" | "Medium" | "High" — mapped to StoryPriority enum in Sprint 5c.
        /// </summary>
        public string Priority { get; set; } = string.Empty;

        public List<GeneratedTask> Tasks { get; set; } = new();
    }
}
