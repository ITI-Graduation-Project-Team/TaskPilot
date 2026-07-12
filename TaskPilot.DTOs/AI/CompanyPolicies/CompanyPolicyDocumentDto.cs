using System;

namespace TaskPilot.DTOs.AI.CompanyPolicies
{
    public class CompanyPolicyDocumentDto
    {
        public Guid DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }
}
