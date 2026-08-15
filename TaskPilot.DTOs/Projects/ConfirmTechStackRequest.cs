using System.Collections.Generic;

namespace TaskPilot.DTOs.Projects
{
    public class ConfirmTechStackRequest
    {
        public List<string> TechStack { get; set; } = new();
    }
}
