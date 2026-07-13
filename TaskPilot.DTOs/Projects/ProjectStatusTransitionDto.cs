using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Projects
{
    public class ProjectStatusTransitionDto
    {
        public ProjectStatus FromStatus { get; set; }
        public ProjectStatus ToStatus { get; set; }
    }
}
