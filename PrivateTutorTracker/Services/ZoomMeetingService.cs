using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PrivateTutorTracker.Models;

namespace PrivateTutorTracker.Services;

public class ZoomMeetingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ZoomAuthService _authService;
    private readonly ILogger<ZoomMeetingService> _logger;

    public ZoomMeetingService(
        IHttpClientFactory httpClientFactory,
        ZoomAuthService authService,
        ILogger<ZoomMeetingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
        _logger = logger;
    }

    public async Task<ZoomMeetingResponse?> CreateMeetingAsync(string topic, DateTime startTime, int durationMinutes, string? agenda = null)
    {
        var request = new ZoomMeetingRequest
        {
            Topic = topic,
            StartTime = startTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Duration = durationMinutes,
            Timezone = "Europe/Istanbul",
            Agenda = agenda,
            Settings = new ZoomMeetingSettings()
        };

        return await SendZoomRequestAsync<ZoomMeetingResponse>(HttpMethod.Post, "https://api.zoom.us/v2/users/me/meetings", request);
    }

    public async Task<ZoomMeetingResponse?> GetMeetingAsync(long meetingId)
    {
        return await SendZoomRequestAsync<ZoomMeetingResponse>(HttpMethod.Get, $"https://api.zoom.us/v2/meetings/{meetingId}");
    }

    public async Task<bool> UpdateMeetingAsync(long meetingId, DateTime startTime, int durationMinutes, string timezone = "Europe/Istanbul")
    {
        var body = new
        {
            start_time = startTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
            duration = durationMinutes,
            timezone
        };

        return await SendZoomRequestAsync(HttpMethod.Patch, $"https://api.zoom.us/v2/meetings/{meetingId}", body);
    }

    public async Task<bool> DeleteMeetingAsync(long meetingId)
    {
        return await SendZoomRequestAsync(HttpMethod.Delete, $"https://api.zoom.us/v2/meetings/{meetingId}");
    }

    private async Task<T?> SendZoomRequestAsync<T>(HttpMethod method, string url, object? body = null)
    {
        var responseContent = await SendZoomRequestCoreAsync(method, url, body);
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(responseContent);
    }

    private async Task<bool> SendZoomRequestAsync(HttpMethod method, string url, object? body = null)
    {
        var responseContent = await SendZoomRequestCoreAsync(method, url, body, allowEmptySuccess: true);
        return responseContent != null;
    }

    private async Task<string?> SendZoomRequestCoreAsync(HttpMethod method, string url, object? body, bool allowEmptySuccess = false)
    {
        try
        {
            var token = await _authService.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            if (body != null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Zoom request failed. Method: {Method}, Url: {Url}, Status: {StatusCode}, Response: {Response}", method, url, response.StatusCode, content);
                return null;
            }

            return string.IsNullOrWhiteSpace(content) && allowEmptySuccess ? string.Empty : content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zoom request could not be completed. Method: {Method}, Url: {Url}", method, url);
            return null;
        }
    }
}
