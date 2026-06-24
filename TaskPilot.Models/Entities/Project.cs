using TaskPilot.Models.Common;

namespace TaskPilot.Models.Entities
{
    public class Project : AuditableEntity<Guid>
    {
        public string NameEn { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public Guid ManagerId { get; set; }
        public Guid CompanyId { get; set; }
        public ProjectManager Manager { get; set; } = null!;
        public Company Company { get; set; } = null!;
        public ICollection<Sprint> Sprints { get; set; } = new List<Sprint>();
        public ICollection<Policy> Policies { get; set; } = new List<Policy>();
        public ICollection<ProjectEmployee> ProjectEmployees { get; set; } = new List<ProjectEmployee>();
    }
}