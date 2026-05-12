namespace TaskPilot.DTOs.Auth
{
    public class InvitationInfoResponse
    {
        public string Email { get; set; }
            = string.Empty;

        public string CompanyName { get; set; }
            = string.Empty;

        public bool UserExists { get; set; }

        public string Token { get; set; }
            = string.Empty;
    }
}
