using System.Collections.Generic;

namespace TaskPilot.DTOs.Company
{
    public class InviteEmployeesRequest
    {
        public List<string> Emails { get; set; } = new();
    }
}
