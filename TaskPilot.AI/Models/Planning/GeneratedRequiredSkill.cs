namespace TaskPilot.AI.Models.Planning
{
    public class GeneratedRequiredSkill
    {
        public string SkillName { get; set; } = string.Empty;
        public string RequiredLevel { get; set; } = string.Empty;
    }

    public sealed class SkillEnrichmentTaskInput
    {
        public Guid TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public sealed class GeneratedTaskRequiredSkills
    {
        public Guid TaskId { get; set; }
        public List<GeneratedRequiredSkill> Skills { get; set; } = new();
    }
}
