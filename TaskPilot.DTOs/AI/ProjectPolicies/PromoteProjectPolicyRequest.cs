using System;
using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.AI.ProjectPolicies
{
    public class PromoteProjectPolicyRequest
    {
        [Required]
        public Guid RequirementSessionId { get; set; }
        [Required]
        public Guid ProjectId { get; set; }
    }
}
