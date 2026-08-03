namespace TaskPilot.AI.Models.Planning
{
    public class GeneratedStoryTasksBatch
    {
        public List<GeneratedStoryTasks> StoryTasks { get; set; } = new();
    }

    public class GeneratedStoryTasks
    {
        public string StoryId { get; set; } = string.Empty;
        public List<GeneratedTask> Tasks { get; set; } = new();
    }
}
