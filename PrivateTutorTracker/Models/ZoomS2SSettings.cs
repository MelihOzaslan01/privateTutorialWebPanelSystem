namespace PrivateTutorTracker.Models;

/// <summary>
/// Zoom Server-to-Server OAuth ayarları
/// </summary>
public class ZoomS2SSettings
{
    /// <summary>
    /// Zoom Account ID
    /// </summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// Zoom Client ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Zoom Client Secret
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
}
