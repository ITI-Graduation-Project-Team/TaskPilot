namespace TaskPilot.DTOs.Auth
{
    public class ResetPasswordDTO
    {
        public string OTP {  get; set; }
        public string Email { get; set; }
        public string Password { get; set; }

    }
}
