using System;
using System.Collections.Generic;

namespace TaskPilot.Models.Configurations
{
    public class RequirementValidationOptions
    {
        public int DefaultThreshold { get; set; } = 80;
        public Dictionary<Guid, int> TenantOverrides { get; set; } = new Dictionary<Guid, int>();
    }
}
