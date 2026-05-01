using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.Auth
{
    /// <summary>
    /// Input DTO for user login.
    /// </summary>
    public class LoginDTO
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
