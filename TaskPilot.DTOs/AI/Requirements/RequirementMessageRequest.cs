using Microsoft.AspNetCore.Http;

namespace TaskPilot.DTOs.AI.Requirements
{
    public class RequirementMessageRequest
    {
        public Guid? SessionId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}