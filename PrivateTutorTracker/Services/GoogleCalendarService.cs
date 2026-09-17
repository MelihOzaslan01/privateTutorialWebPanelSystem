using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;

namespace PrivateTutorTracker.Services;

public class GoogleCalendarService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleCalendarService> _logger;
    private CalendarService? _service;
    private string? _calendarId;

    public GoogleCalendarService(IConfiguration configuration, ILogger<GoogleCalendarService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string?> AddLessonEventAsync(
        string title,
        string description,
        DateTime start,
        DateTime end,
        string? zoomJoinUrl = null,
        string? zoomPassword = null)
    {
        try
        {
            var service = GetService();
            if (service == null || string.IsNullOrWhiteSpace(_calendarId))
            {
                return null;
            }

            var newEvent = CreateEvent(title, description, start, end, zoomJoinUrl, zoomPassword);
            var created = await service.Events.Insert(newEvent, _calendarId).ExecuteAsync();
            return created.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Calendar event could not be created.");
            return null;
        }
    }

    public async Task<bool> UpdateLessonEventAsync(
        string eventId,
        string title,
        string description,
        DateTime start,
        DateTime end,
        string? zoomJoinUrl = null,
        string? zoomPassword = null)
    {
        try
        {
            var service = GetService();
            if (service == null || string.IsNullOrWhiteSpace(_calendarId))
            {
                return false;
            }

            var updatedEvent = CreateEvent(title, description, start, end, zoomJoinUrl, zoomPassword);
            await service.Events.Update(updatedEvent, _calendarId, eventId).ExecuteAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Calendar event could not be updated. EventId: {EventId}", eventId);
            return false;
        }
    }

    public async Task<bool> DeleteLessonEventAsync(string eventId)
    {
        try
        {
            var service = GetService();
            if (service == null || string.IsNullOrWhiteSpace(_calendarId))
            {
                return false;
            }

            await service.Events.Delete(_calendarId, eventId).ExecuteAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Calendar event could not be deleted. EventId: {EventId}", eventId);
            return false;
        }
    }

    private CalendarService? GetService()
    {
        if (_service != null)
        {
            return _service;
        }

        _calendarId = _configuration["GoogleCalendar:CalendarId"];
        var keyFilePath = _configuration["GoogleCalendar:ServiceAccountKeyFile"];

        if (string.IsNullOrWhiteSpace(_calendarId) || string.IsNullOrWhiteSpace(keyFilePath))
        {
            _logger.LogWarning("Google Calendar settings are missing.");
            return null;
        }

        if (!Path.IsPathRooted(keyFilePath))
        {
            keyFilePath = Path.Combine(AppContext.BaseDirectory, keyFilePath);
        }

        if (!File.Exists(keyFilePath))
        {
            _logger.LogWarning("Google service account key file was not found: {Path}", keyFilePath);
            return null;
        }

        using var stream = new FileStream(keyFilePath, FileMode.Open, FileAccess.Read);
        var credential = GoogleCredential.FromStream(stream)
            .CreateScoped(CalendarService.Scope.Calendar);

        _service = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "PrivateTutor Tracker"
        });

        return _service;
    }

    private static Event CreateEvent(string title, string description, DateTime start, DateTime end, string? zoomJoinUrl, string? zoomPassword)
    {
        if (!string.IsNullOrWhiteSpace(zoomJoinUrl))
        {
            description += $"\n\nZoom Linki: {zoomJoinUrl}";
        }

        if (!string.IsNullOrWhiteSpace(zoomPassword))
        {
            description += $"\nZoom Sifresi: {zoomPassword}";
        }

        return new Event
        {
            Summary = title,
            Description = description,
            Start = new EventDateTime { DateTimeDateTimeOffset = start, TimeZone = "Europe/Istanbul" },
            End = new EventDateTime { DateTimeDateTimeOffset = end, TimeZone = "Europe/Istanbul" },
            Reminders = new Event.RemindersData
            {
                UseDefault = false,
                Overrides = new List<EventReminder>
                {
                    new() { Method = "popup", Minutes = 15 },
                    new() { Method = "email", Minutes = 30 }
                }
            }
        };
    }
}
