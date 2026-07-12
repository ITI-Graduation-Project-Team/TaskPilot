using System;
using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.AI.Knowledge
{
    public class KnowledgeAskRequest
    {
        public Guid? RequirementSessionId { get; set; }
        
        public Guid? ProjectId { get; set; }
        
        public Guid? CompanyId { get; set; }
        
        [Required]
        public TaskPilot.Models.Enums.KnowledgeCollectionType CollectionType { get; set; }

        [Required]
        [MinLength(1)]
        public string Question { get; set; } = string.Empty;
    }
}
