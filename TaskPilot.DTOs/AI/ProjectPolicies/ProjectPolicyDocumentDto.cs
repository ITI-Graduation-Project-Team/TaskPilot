using System;

namespace TaskPilot.DTOs.AI.ProjectPolicies
{
    public class ProjectPolicyDocumentDto
    {
        public Guid PolicyId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime UploadDate { get; set; }
        public string Category { get; set; } = string.Empty;
        public int Version { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string AiStatus { get; set; } = string.Empty;
    }
}
