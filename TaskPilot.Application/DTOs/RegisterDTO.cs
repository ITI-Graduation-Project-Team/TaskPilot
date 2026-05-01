using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.Application.DTOs
{
    public class RegisterDTO
    {
        public string FirstNameEn { get; set; } = string.Empty;
        public string LastNameEn { get; set; } = string.Empty;
        public string FirstNameAr { get; set; } = string.Empty;
        public string LastNameAr { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
