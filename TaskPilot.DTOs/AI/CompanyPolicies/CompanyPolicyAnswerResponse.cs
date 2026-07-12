using System.Collections.Generic;

namespace TaskPilot.DTOs.AI.CompanyPolicies
{
    public class CompanyPolicyAnswerResponse
    {
        public string Answer { get; set; } = string.Empty;
        public List<CompanyPolicySourceDto> Sources { get; set; } = new();
    }

    public class CompanyPolicySourceDto
    {
        public string FileName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
}
