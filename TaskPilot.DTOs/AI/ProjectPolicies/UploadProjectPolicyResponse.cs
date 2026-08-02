using System;

namespace TaskPilot.DTOs.AI.ProjectPolicies
{
    public class UploadProjectPolicyResponse
    {
        public Guid DocumentId { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ChunksCreated { get; set; }
    }
}
