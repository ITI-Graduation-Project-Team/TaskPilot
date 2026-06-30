using System;
using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.AI.Knowledge
{
    public class KnowledgeAskRequest
    {
        [Required]
        public Guid SessionId { get; set; }

        [Required]
        [MinLength(1)]
        public string Question { get; set; } = string.Empty;
    }
}
