using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.DTOs
{
    public class ResetPasswordDTO
    {
        public string OTP {  get; set; }
        public string Email { get; set; }
        public string Password { get; set; }

    }
}
