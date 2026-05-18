using System;

namespace TaskPilot.DTOs.Projects
{
    public class CreateProjectDto
    {
        public string NameEn { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public Guid ManagerId { get; set; }
        public Guid CompanyId { get; set; }
    }
}
