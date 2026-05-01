namespace TaskPilot.DTOs.Auth
{
    /// <summary>
    /// Output DTO returned after successful authentication.
    /// </summary>
    public class AuthResponseDTO
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }
}
