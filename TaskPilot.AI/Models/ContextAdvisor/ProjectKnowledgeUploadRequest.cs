using Microsoft.AspNetCore.Http;

namespace TaskPilot.AI.Models.ContextAdvisor
{
    public class ProjectKnowledgeUploadRequest
    {
        public Guid? ProjectId { get; set; }

        public bool IsAvailableToContextSummarizer { get; set; } = true;

        public IFormFile File { get; set; } = null!;
    }
}
