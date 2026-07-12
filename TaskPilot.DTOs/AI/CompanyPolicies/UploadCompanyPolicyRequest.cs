using System;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.AI.CompanyPolicies
{
    public class UploadCompanyPolicyRequest
    {
        [Required]
        public Guid CompanyId { get; set; }

        [Required]
        public IFormFile File { get; set; } = null!;
    }
}
