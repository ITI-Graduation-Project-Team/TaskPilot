using System.Collections.Generic;

namespace TaskPilot.DTOs.Company
{
    public class InviteEmployeesResponse
    {
        public int SentCount { get; set; }
        public List<string> InvitedEmails { get; set; } = new();
        public List<SkippedEmployeeDto> SkippedEmployees { get; set; } = new();
    }
}
