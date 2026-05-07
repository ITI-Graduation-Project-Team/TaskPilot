using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.Roles
{
    public class RoleDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public List<string> Permissions { get; set; } = new List<string>();
    }
}
