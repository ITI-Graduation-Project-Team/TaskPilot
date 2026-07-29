using System;
using Microsoft.AspNetCore.Http;

namespace TaskPilot.DTOs.AI.ProjectPolicies
{
    public class IngestProjectPolicyRequest
    {
        public Guid? ProjectId { get; set; }
        public Guid? RequirementSessionId { get; set; }
        public IFormFile? File { get; set; }
        public string? TitleEn { get; set; }
        public string? DocumentUrl { get; set; }
        public string? CloudinaryPublicId { get; set; }
        public bool SkipCloudUpload { get; set; }
    }
}
