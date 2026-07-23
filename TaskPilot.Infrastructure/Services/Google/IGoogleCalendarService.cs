namespace TaskPilot.Infrastructure.Services.Google;

public interface IGoogleCalendarService
{
 
     //recieve link which employee or manager will click to login to google and give us permission to access his calendar
    string GetGoogleLoginUrl();

    //take the reurned code from google and exchange it for the refresh token (and save it)
    Task<bool> ExchangeCodeForTokenAsync(string code, Guid userId);
}