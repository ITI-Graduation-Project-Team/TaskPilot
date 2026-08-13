using System.Text.Json;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Projects
{
    public sealed class ProjectSetupDto
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public ProjectSetupOverallStatus OverallStatus { get; set; }
        public TechStackSetupDto TechStack { get; set; } = new();
        public SetupJobDto Wbs { get; set; } = new();
        public SetupJobDto Skills { get; set; } = new();
    }

    public sealed class TechStackSetupDto
    {
        public TechStackSetupStatus Status { get; set; }
        public JsonElement? Suggestion { get; set; }
        public List<string> ConfirmedStack { get; set; } = new();
        public List<string> Platforms { get; set; } = new();
        public string ProjectType { get; set; } = string.Empty;
        public string? Error { get; set; }
    }

    public sealed class SetupJobDto
    {
        public BackgroundSetupStatus Status { get; set; }
        public string? JobId { get; set; }
        public int AttemptCount { get; set; }
        public int ItemsCreated { get; set; }
        public int SecondaryItemsCreated { get; set; }
        public int ItemsSkipped { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Error { get; set; }
    }
}
