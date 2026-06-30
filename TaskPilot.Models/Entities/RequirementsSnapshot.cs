using System.Collections.Generic;

namespace TaskPilot.Models.Entities
{
    public class RequirementsSnapshot
    {
        public List<string> BusinessRequirements { get; set; } = new List<string>();
        public List<string> TechnicalRequirements { get; set; } = new List<string>();
        public List<string> Constraints { get; set; } = new List<string>();
        public List<string> Integrations { get; set; } = new List<string>();
        public List<string> ScaleRequirements { get; set; } = new List<string>();
    }
}
