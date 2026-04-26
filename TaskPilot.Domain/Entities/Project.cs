using System.Globalization;
using TaskPilot.Domain.Common;

namespace TaskPilot.Domain.Entities
{
    public class Project : AuditableEntity
    {
        public string NameEn { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public Guid ManagerId { get; set; }
        public User Manager { get; set; }
        public ICollection<Sprint> Sprints { get; set; } = new List<Sprint>();
        public ICollection<ProjectPolicy> Policies { get; set; } = new List<ProjectPolicy>();
    }
}