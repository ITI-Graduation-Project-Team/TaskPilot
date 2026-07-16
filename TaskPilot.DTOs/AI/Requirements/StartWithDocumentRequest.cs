using Microsoft.AspNetCore.Http;

namespace TaskPilot.DTOs.AI.Requirements
{
    public class StartWithDocumentRequest
    {
        public IFormFile? File { get; set; }
    }
}
