using System.Collections.Generic;

namespace TaskPilot.DTOs.Roles
{
    public class UpdateRolePermissionsDto
    {
        public List<string> Permissions { get; set; } = new List<string>();
    }
}
