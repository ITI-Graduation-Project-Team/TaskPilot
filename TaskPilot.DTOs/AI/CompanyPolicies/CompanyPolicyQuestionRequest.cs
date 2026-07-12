using System;
using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.AI.CompanyPolicies
{
    public class CompanyPolicyQuestionRequest
    {
        [Required]
        public Guid CompanyId { get; set; }

        [Required]
        [MinLength(3)]
        public string Question { get; set; } = string.Empty;
    }
}
