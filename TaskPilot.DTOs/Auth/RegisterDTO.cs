using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.Auth
{
    /// <summary>
    /// Input DTO for user registration.
    /// </summary>
    public class RegisterDTO
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string FirstNameEn { get; set; } = string.Empty;

        [Required]
        public string LastNameEn { get; set; } = string.Empty;

        public string FirstNameAr { get; set; } = string.Empty;
        public string LastNameAr { get; set; } = string.Empty;

        [Required]
        public Guid CompanyId { get; set; }
    }
}
