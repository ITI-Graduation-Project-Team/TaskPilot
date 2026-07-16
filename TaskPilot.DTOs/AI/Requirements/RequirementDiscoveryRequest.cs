using System;
using Microsoft.AspNetCore.Http;

namespace TaskPilot.DTOs.AI.Requirements
{
    public class RequirementDiscoveryRequest
    {
        public Guid? SessionId { get; set; }
        public string? Message { get; set; }
        public IFormFileCollection? Documents { get; set; }
    }
}
