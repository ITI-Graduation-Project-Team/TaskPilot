using System.ComponentModel.DataAnnotations;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Auth
{
    public class GoogleCompleteSignupDTO
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }
    }
}
