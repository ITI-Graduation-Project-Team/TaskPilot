using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.DTOs.Auth
{
    public class RegisterResponseDTO
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Message {  get; set; } = string.Empty;
    }
}
