namespace TaskPilot.AI.Models.Planning
{
    public class GeneratedTask
    {
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public string? AcceptanceCriteriaEn { get; set; }
        public string? AcceptanceCriteriaAr { get; set; }

        /// <summary>
        /// "Small" | "Medium" | "Large" — mapped to EffortSize enum in Sprint 5c.
        /// </summary>
        public string EffortSize { get; set; } = string.Empty;

        /// <summary>
        /// "Technical" | "NonTechnical" — mapped to TaskType enum in Sprint 5c.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Must be consistent with EffortSize:
        /// Small = 1-4 hrs, Medium = 4-16 hrs, Large = 16+ hrs.
        /// </summary>
        public decimal EstimatedHours { get; set; }

        /// <summary>
        /// "Low" | "Medium" | "High" — mapped to TaskPriority enum in Sprint 5c.
        /// </summary>
        public string Priority { get; set; } = string.Empty;
    }
}
