namespace TaskPilot.AI.Models.Planning
{
    /// <summary>
    /// Top-level output of WBSGenerationAgent.
    /// Contains a flat list of UserStories for the whole project.
    /// No Sprint assignment at this stage — that comes later.
    /// </summary>
    public class GeneratedWbs
    {
        public List<GeneratedUserStory> UserStories { get; set; } = new();
    }
}
