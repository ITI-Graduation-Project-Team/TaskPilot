using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.DTOs.Auth
{
    public class ConfirmEmailDTO
    {
        public string Email { get; set; }
        public string OTP { get; set; } 
    }
}
