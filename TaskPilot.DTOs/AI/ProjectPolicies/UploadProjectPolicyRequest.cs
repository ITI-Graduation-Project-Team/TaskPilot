using System;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.AI.ProjectPolicies
{
    public class UploadProjectPolicyRequest
    {
        public Guid? ProjectId { get; set; }
        public Guid? RequirementSessionId { get; set; }
        [Required]
        public IFormFile File { get; set; } = null!;
    }
}
