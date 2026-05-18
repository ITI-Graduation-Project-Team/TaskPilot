using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.Models.Entities
{
    public class RefreshToken
    {
        public Guid Id { get; set; }
        public string Token { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public bool IsRevoked => RevokedAt.HasValue;
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        public bool IsInactive => DateTime.UtcNow > LastActivityAt.AddHours(8);
        public bool IsActive => !IsRevoked && !IsExpired && !IsInactive;
    }
}
