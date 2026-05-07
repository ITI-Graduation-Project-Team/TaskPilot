using System.Collections.Generic;

namespace TaskPilot.DTOs.Roles
{
    public class PermissionModuleDto
    {
        public string ModuleName { get; set; }
        public List<PermissionItemDto> Permissions { get; set; } = new List<PermissionItemDto>();
    }

    public class PermissionItemDto
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
