using TaskPilot.Domain.Common;

namespace TaskPilot.Domain.Entities
{
    public class ProjectEntity : AuditableEntity<Guid>
    {
        public string NameEn { get; set; }
        public string NameAr { get; set; }

        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }

        public Guid ManagerId { get; private set; }      
    }
}
