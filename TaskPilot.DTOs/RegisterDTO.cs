using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs
{
    public class RegisterDTO
    {
        [Required]
        public string FirstNameEn { get; set; } = string.Empty;
        [Required]
        public string LastNameEn { get; set; } = string.Empty;
        [Required]
        public string FirstNameAr { get; set; } = string.Empty;
        [Required]
        public string LastNameAr { get; set; } = string.Empty;
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
