using System;
using Microsoft.AspNetCore.Http;

namespace TaskPilot.DTOs.AI.CompanyPolicies
{
    public class IngestCompanyPolicyRequest
    {
        public Guid CompanyId { get; set; }
        
        // Either File or text content should be provided
        public IFormFile? File { get; set; }
        public string? ContentEn { get; set; }
        public string? ContentAr { get; set; }

        public string? TitleEn { get; set; }
        public string? TitleAr { get; set; }

        public string? DocumentUrl { get; set; }
        public string? CloudinaryPublicId { get; set; }
        public bool SkipCloudUpload { get; set; }
    }
}
