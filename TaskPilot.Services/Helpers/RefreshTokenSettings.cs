using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.Services.Helpers
{
    public class RefreshTokenSettings
    {
        public int ExpiryDays { get; set; } = 7;
        public int InactivityHours { get; set; } = 8;
    }
}
