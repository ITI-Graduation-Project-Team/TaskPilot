namespace TaskPilot.Infrastructure.Settings
{
    public class RefreshTokenSettings
    {
        public int ExpiryDays { get; set; } = 7;
        public int InactivityHours { get; set; } = 8;
    }
}
