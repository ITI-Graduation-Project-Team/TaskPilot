using System;
using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.AI.ProjectPolicies
{
    public class ProjectPolicyQuestionRequest
    {
        public Guid? ProjectId { get; set; }
        public Guid? RequirementSessionId { get; set; }
        [Required, MinLength(3)]
        public string Question { get; set; } = string.Empty;
    }
}
