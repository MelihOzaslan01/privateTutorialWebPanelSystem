using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateTutorTracker.Data;
using PrivateTutorTracker.Models;
using PrivateTutorTracker.Services;

namespace PrivateTutorTracker.Controllers;

/// <summary>
/// Zoom entegrasyonu test endpoint'leri
/// </summary>
[Authorize]
public class ZoomTestController : Controller
{
    private readonly ZoomMeetingService _zoomMeetingService;
    private readonly ZoomAuthService _zoomAuthService;
    private readonly ZoomS2SSettings _zoomSettings;
    private readonly AppDbContext _context;
    private readonly ILogger<ZoomTestController> _logger;

    public ZoomTestController(
        ZoomMeetingService zoomMeetingService,
        ZoomAuthService zoomAuthService,
        ZoomS2SSettings zoomSettings,
        AppDbContext context,
        ILogger<ZoomTestController> logger)
    {
        _zoomMeetingService = zoomMeetingService;
        _zoomAuthService = zoomAuthService;
        _zoomSettings = zoomSettings;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Zoom ayarlarını ve token'ı test et
    /// Endpoint: /ZoomTest/TestConfig
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TestConfig()
    {
        try
        {
            _logger.LogInformation("Testing Zoom configuration...");

            // 1. Ayarları kontrol et
            var hasAccountId = !string.IsNullOrEmpty(_zoomSettings.AccountId);
            var hasClientId = !string.IsNullOrEmpty(_zoomSettings.ClientId);
            var hasClientSecret = !string.IsNullOrEmpty(_zoomSettings.ClientSecret);

            _logger.LogInformation($"AccountId present: {hasAccountId}");
            _logger.LogInformation($"ClientId present: {hasClientId}");
            _logger.LogInformation($"ClientSecret present: {hasClientSecret}");

            if (!hasAccountId || !hasClientId || !hasClientSecret)
            {
                return Json(new
                {
                    success = false,
                    message = "Zoom ayarları eksik!",
                    config = new
                    {
                        hasAccountId,
                        hasClientId,
                        hasClientSecret,
                        accountIdLength = _zoomSettings.AccountId?.Length ?? 0,
                        clientIdLength = _zoomSettings.ClientId?.Length ?? 0,
                        clientSecretLength = _zoomSettings.ClientSecret?.Length ?? 0
                    }
                });
            }

            // 2. Token almayı dene
            _logger.LogInformation("Attempting to get access token...");
            var token = await _zoomAuthService.GetAccessTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                return Json(new
                {
                    success = false,
                    message = "Access token alınamadı. OAuth ayarlarını kontrol edin.",
                    config = new
                    {
                        hasAccountId = true,
                        hasClientId = true,
                        hasClientSecret = true,
                        accountIdLength = _zoomSettings.AccountId.Length,
                        clientIdLength = _zoomSettings.ClientId.Length
                    }
                });
            }

            return Json(new
            {
                success = true,
                message = "Zoom ayarları doğru ve token başarıyla alındı!",
                tokenLength = token.Length,
                tokenPrefix = token.Substring(0, Math.Min(10, token.Length)) + "..."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Zoom configuration");
            return Json(new
            {
                success = false,
                message = $"Hata: {ex.Message}",
                stackTrace = ex.StackTrace
            });
        }
    }

    /// <summary>
    /// Test amaçlı Zoom toplantısı oluşturma
    /// Endpoint: /ZoomTest/TestZoom
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TestZoom()
    {
        try
        {
            _logger.LogInformation("Test Zoom meeting creation started");
            _logger.LogInformation("Testing Zoom configuration...");

            // Test toplantısı için parametreler
            var topic = "Test Dersi - Matematik";
            var startTime = DateTime.Now.AddHours(1); // 1 saat sonrası
            var durationMinutes = 40;
            var agenda = "Bu bir test toplantısıdır. Matematik dersi için oluşturulmuştur.";

            _logger.LogInformation($"Creating meeting: {topic}, Start: {startTime}, Duration: {durationMinutes}");

            // Zoom toplantısı oluştur
            var meetingResponse = await _zoomMeetingService.CreateMeetingAsync(
                topic,
                startTime,
                durationMinutes,
                agenda
            );

            if (meetingResponse == null)
            {
                _logger.LogError("Meeting response is null - check application logs for details");
                return Json(new
                {
                    success = false,
                    message = "Zoom toplantısı oluşturulamadı. Lütfen application loglarını kontrol edin. ZoomAuthService veya ZoomMeetingService'de hata oluşmuş olabilir."
                });
            }

            // Başarılı sonuç döndür
            return Json(new
            {
                success = true,
                message = "Zoom toplantısı başarıyla oluşturuldu!",
                meeting = new
                {
                    id = meetingResponse.Id,
                    topic = meetingResponse.Topic,
                    joinUrl = meetingResponse.JoinUrl,
                    startUrl = meetingResponse.StartUrl,
                    password = meetingResponse.Password,
                    startTime = meetingResponse.StartTime,
                    duration = meetingResponse.Duration,
                    timezone = meetingResponse.Timezone,
                    createdAt = meetingResponse.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during test Zoom meeting creation");
            return Json(new
            {
                success = false,
                message = $"Hata: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Zoom toplantısı bilgilerini görüntüleme
    /// Endpoint: /ZoomTest/GetMeeting?meetingId=123456789
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMeeting(long meetingId)
    {
        try
        {
            var meetingResponse = await _zoomMeetingService.GetMeetingAsync(meetingId);

            if (meetingResponse == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Toplantı bulunamadı."
                });
            }

            return Json(new
            {
                success = true,
                meeting = new
                {
                    id = meetingResponse.Id,
                    topic = meetingResponse.Topic,
                    joinUrl = meetingResponse.JoinUrl,
                    startUrl = meetingResponse.StartUrl,
                    password = meetingResponse.Password,
                    startTime = meetingResponse.StartTime,
                    duration = meetingResponse.Duration,
                    timezone = meetingResponse.Timezone
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting Zoom meeting: {meetingId}");
            return Json(new
            {
                success = false,
                message = $"Hata: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Tüm derslerin Zoom ID'lerini listele
    /// Endpoint: /ZoomTest/ListLessonsWithZoom
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListLessonsWithZoom()
    {
        try
        {
            var lessons = await _context.Lessons
                .Include(l => l.Student)
                .Where(l => l.ZoomMeetingId != null)
                .OrderByDescending(l => l.Baslangic)
                .Take(20)
                .Select(l => new
                {
                    l.Id,
                    l.Konu,
                    Student = l.Student != null ? l.Student.AdSoyad : "Bilinmeyen",
                    l.Baslangic,
                    l.ZoomMeetingId,
                    l.GoogleCalendarEventId
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                count = lessons.Count,
                lessons
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                success = false,
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Zoom toplantısını silme (GET de destekler test için)
    /// Endpoint: /ZoomTest/DeleteMeeting?meetingId=123456789
    /// </summary>
    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> DeleteMeeting(long meetingId)
    {
        try
        {
            _logger.LogInformation($"TEST: Deleting Zoom meeting: {meetingId}");

            var result = await _zoomMeetingService.DeleteMeetingAsync(meetingId);

            _logger.LogInformation($"TEST: Delete result: {result}");

            if (!result)
            {
                return Json(new
                {
                    success = false,
                    message = "Toplantı silinemedi. Loglara bakın."
                });
            }

            return Json(new
            {
                success = true,
                message = $"Toplantı başarıyla silindi: {meetingId}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting Zoom meeting: {meetingId}");
            return Json(new
            {
                success = false,
                message = $"Hata: {ex.Message}",
                stackTrace = ex.StackTrace
            });
        }
    }
}
