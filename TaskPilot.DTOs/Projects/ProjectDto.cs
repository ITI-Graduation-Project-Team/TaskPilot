using System;

namespace TaskPilot.DTOs.Projects
{
    public class ProjectDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid ManagerId { get; set; }
        public Guid CompanyId { get; set; }
        public System.Collections.Generic.List<string> TechStack { get; set; } = new();
        public System.Collections.Generic.List<string> PlatformTargets { get; set; } = new();
        public string ProjectType { get; set; } = string.Empty;
    }
}
