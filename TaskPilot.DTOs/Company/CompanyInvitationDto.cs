using System;

namespace TaskPilot.DTOs.Company
{
    public class CompanyInvitationDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public DateTime InvitedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool Accepted { get; set; }
        public string InvitedBy { get; set; } = string.Empty;
    }
}
