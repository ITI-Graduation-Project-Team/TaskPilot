using System.Collections.Generic;

namespace TaskPilot.DTOs.AI.ProjectPolicies
{
    public class ProjectPolicyAnswerResponse
    {
        public string Answer { get; set; } = string.Empty;
        public List<ProjectPolicySourceDto> Sources { get; set; } = new();
    }

    public class ProjectPolicySourceDto
    {
        public string FileName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
}
