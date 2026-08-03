using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Entities;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using TaskPilot.Services.Interfaces.External;

namespace TaskPilot.Infrastructure.Services.Google;

public class GoogleCalendarService : IGoogleCalendarService
{
    private readonly IConfiguration _config;
    private readonly IRepository<User> _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GoogleCalendarService> _logger;

    //give the employee access to project settings (to fetch google keys) and database
    public GoogleCalendarService(
        IConfiguration config,
        IRepository<User> userRepository,
        IUnitOfWork unitOfWork,
        ILogger<GoogleCalendarService> logger)
    {
        _config = config;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }
    //preparing the basic google settings
    private GoogleAuthorizationCodeFlow GetGoogleFlow()
    {
        return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _config["GoogleCalendar:ClientId"],
                ClientSecret = _config["GoogleCalendar:ClientSecret"]
            },
            //ask for permission to modify the calendar
            Scopes = new[] { "https://www.googleapis.com/auth/calendar.events" }
        });
    }

    // execute the first task: generate the login link
    // تنفيذ المهمة الأولى: توليد رابط تسجيل الدخول
    public string GetGoogleLoginUrl(Guid userId)
    {
        var flow = GetGoogleFlow();
        var redirectUri = _config["GoogleCalendar:RedirectUri"];

        //create link that the user will go to
        var request = (global::Google.Apis.Auth.OAuth2.Requests.GoogleAuthorizationCodeRequestUrl)flow.CreateAuthorizationCodeRequest(redirectUri);
        request.State = userId.ToString();
        request.AccessType = "offline";
        request.Prompt = "consent";
        return request.Build().AbsoluteUri;
    }

    public async Task<bool> ExchangeCodeForTokenAsync(string code, Guid userId)
    {
        var flow = GetGoogleFlow();
        var redirectUri = _config["GoogleCalendar:RedirectUri"];

        var tokenResponse = await flow.ExchangeCodeForTokenAsync(
            userId.ToString(),
            code,
            redirectUri,
            CancellationToken.None);

        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.RefreshToken))
        {
            _logger.LogWarning("Google did not return a RefreshToken for user {UserId}. The user may have already authorized before — re-consent was not triggered.", userId);
            return false;
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found when trying to save Google RefreshToken.", userId);
            return false;
        }

        user.GoogleRefreshToken = tokenResponse.RefreshToken;
        _userRepository.Update(user);

        // Persist the token to the database
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Google Calendar token saved successfully for user {UserId}.", userId);
        return true;
    }

    public async Task<string> AddEventToCalendarAsync(Guid userId, string title, string description, DateTime startTime, DateTime endTime)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null || string.IsNullOrEmpty(user.GoogleRefreshToken))
            throw new Exception("المستخدم غير موجود أو لم يقم بـ ربط تقويم جوجل الخاص به.");

        var flow = GetGoogleFlow();
        var token = new TokenResponse { RefreshToken = user.GoogleRefreshToken };
        var credential = new UserCredential(flow, userId.ToString(), token);

        var calendarService = new CalendarService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "TaskPilot"
        });

        var newEvent = new Event()
        {
            Summary = title,
            Description = description,
            Start = new EventDateTime() { DateTimeDateTimeOffset = startTime },
            End = new EventDateTime() { DateTimeDateTimeOffset = endTime },
        };

        var request = calendarService.Events.Insert(newEvent, "primary");
        var createdEvent = await request.ExecuteAsync();

        return createdEvent.HtmlLink; 
    }

    public string GetGoogleLoginUrl()
    {
        throw new NotImplementedException();
    }
}