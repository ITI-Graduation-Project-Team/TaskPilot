using System;

namespace TaskPilot.DTOs.AI.CompanyPolicies
{
    public class UploadCompanyPolicyResponse
    {
        public Guid DocumentId { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ChunksCreated { get; set; }
    }
}
