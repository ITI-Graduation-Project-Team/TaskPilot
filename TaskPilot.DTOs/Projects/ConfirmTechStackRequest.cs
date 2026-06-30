using System.Collections.Generic;

namespace TaskPilot.DTOs.Projects
{
    public class ConfirmTechStackRequest
    {
        public List<string> TechStack { get; set; } = new();
        public List<string> PlatformTargets { get; set; } = new();
        public string ProjectType { get; set; } = string.Empty;
    }
}
