using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PrivateTutorTracker.Models;

namespace PrivateTutorTracker.Services;

public class ZoomAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ZoomS2SSettings _settings;
    private readonly ILogger<ZoomAuthService> _logger;
    private string? _accessToken;
    private DateTime _expiresAtUtc;

    public ZoomAuthService(
        IHttpClientFactory httpClientFactory,
        ZoomS2SSettings settings,
        ILogger<ZoomAuthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _logger = logger;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && DateTime.UtcNow < _expiresAtUtc)
        {
            return _accessToken;
        }

        if (string.IsNullOrWhiteSpace(_settings.AccountId) ||
            string.IsNullOrWhiteSpace(_settings.ClientId) ||
            string.IsNullOrWhiteSpace(_settings.ClientSecret))
        {
            _logger.LogWarning("Zoom settings are missing.");
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://zoom.us/oauth/token?grant_type=account_credentials&account_id={Uri.EscapeDataString(_settings.AccountId)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            using var response = await client.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Zoom token request failed. Status: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
                return null;
            }

            var tokenResponse = JsonSerializer.Deserialize<ZoomTokenResponse>(responseContent);
            if (string.IsNullOrWhiteSpace(tokenResponse?.AccessToken))
            {
                return null;
            }

            _accessToken = tokenResponse.AccessToken;
            _expiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, tokenResponse.ExpiresIn - 60));
            return _accessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zoom access token could not be retrieved.");
            return null;
        }
    }
}
