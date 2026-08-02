using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{
    public class Policy : AuditableEntity<Guid>
    {
        public PolicyScope Scope { get; set; }
        public Guid? CompanyId { get; set; }
        public Company? Company { get; set; }
        public Guid? ProjectId { get; set; }
        public Project? Project { get; set; }
        public Guid? RequirementSessionId { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string? ContentEn { get; set; }
        public string? ContentAr { get; set; }
        public string? DocumentUrl { get; set; }
        
        // External ID for Cloudinary asset
        public string? CloudinaryPublicId { get; set; }
        
        // Deterministic ID for Qdrant Vector DB
        public Guid? DocumentId { get; set; }
        
        // Obsolete: Legacy representation
        public string? DocumentPublicId { get; set; }
        
        public AiProcessingStatus AiStatus { get; set; } = AiProcessingStatus.Pending;
        public string? AiProcessingError { get; set; }
        public int VersionNumber { get; set; } = 1;
        public bool IsActive { get; set; } = true;
    }
}
