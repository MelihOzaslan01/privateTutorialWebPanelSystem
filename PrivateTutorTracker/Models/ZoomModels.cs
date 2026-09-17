using System.Text.Json.Serialization;

namespace PrivateTutorTracker.Models;

/// <summary>
/// Zoom OAuth Token Response
/// </summary>
public class ZoomTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
}

/// <summary>
/// Zoom Meeting Request Body
/// </summary>
public class ZoomMeetingRequest
{
    /// <summary>
    /// Toplantı konusu
    /// </summary>
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Toplantı tipi (1: Anlık, 2: Zamanlanmış, 3: Tekrarlayan, 8: Tekrarlayan-sabit zaman)
    /// </summary>
    [JsonPropertyName("type")]
    public int Type { get; set; } = 2; // 2 = Zamanlanmış toplantı

    /// <summary>
    /// Başlangıç zamanı (ISO 8601 format, UTC)
    /// </summary>
    [JsonPropertyName("start_time")]
    public string StartTime { get; set; } = string.Empty;

    /// <summary>
    /// Süre (dakika)
    /// </summary>
    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    /// <summary>
    /// Saat dilimi
    /// </summary>
    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = "Europe/Istanbul";

    /// <summary>
    /// Toplantı açıklaması
    /// </summary>
    [JsonPropertyName("agenda")]
    public string? Agenda { get; set; }

    /// <summary>
    /// Toplantı ayarları
    /// </summary>
    [JsonPropertyName("settings")]
    public ZoomMeetingSettings? Settings { get; set; }
}

/// <summary>
/// Zoom Meeting Settings
/// </summary>
public class ZoomMeetingSettings
{
    /// <summary>
    /// Host video açık olsun mu
    /// </summary>
    [JsonPropertyName("host_video")]
    public bool HostVideo { get; set; } = true;

    /// <summary>
    /// Katılımcı videosu açık olsun mu
    /// </summary>
    [JsonPropertyName("participant_video")]
    public bool ParticipantVideo { get; set; } = true;

    /// <summary>
    /// Katılımcılar katılırken sessize alınsın mı
    /// </summary>
    [JsonPropertyName("mute_upon_entry")]
    public bool MuteUponEntry { get; set; } = false;

    /// <summary>
    /// Bekleme odası kullanılsın mı
    /// </summary>
    [JsonPropertyName("waiting_room")]
    public bool WaitingRoom { get; set; } = false;

    /// <summary>
    /// Toplantıya katılmak için onay gereksin mi
    /// </summary>
    [JsonPropertyName("approval_type")]
    public int ApprovalType { get; set; } = 2; // 0: Otomatik onay, 1: Manuel onay, 2: Kayıt gerekmez
}

/// <summary>
/// Zoom Meeting Response
/// </summary>
public class ZoomMeetingResponse
{
    /// <summary>
    /// Toplantı ID
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Toplantı numarası (string olarak)
    /// </summary>
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = string.Empty;

    /// <summary>
    /// Katılım linki
    /// </summary>
    [JsonPropertyName("join_url")]
    public string JoinUrl { get; set; } = string.Empty;

    /// <summary>
    /// Host başlangıç linki
    /// </summary>
    [JsonPropertyName("start_url")]
    public string StartUrl { get; set; } = string.Empty;

    /// <summary>
    /// Toplantı şifresi
    /// </summary>
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Toplantı konusu
    /// </summary>
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Başlangıç zamanı
    /// </summary>
    [JsonPropertyName("start_time")]
    public string StartTime { get; set; } = string.Empty;

    /// <summary>
    /// Süre (dakika)
    /// </summary>
    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    /// <summary>
    /// Saat dilimi
    /// </summary>
    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = string.Empty;

    /// <summary>
    /// Toplantı oluşturulma zamanı
    /// </summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;
}
