namespace PrivateTutorTracker.Models;

/// <summary>
/// Veritabanı bilgi görüntüleme view model
/// </summary>
public class DatabaseInfoViewModel
{
    /// <summary>
    /// Veritabanına bağlanabilir mi
    /// </summary>
    public bool CanConnect { get; set; }

    /// <summary>
    /// Veritabanı bağlantı string'i
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Uygulanmış migration'lar
    /// </summary>
    public IEnumerable<string> AppliedMigrations { get; set; } = new List<string>();

    /// <summary>
    /// Bekleyen migration'lar
    /// </summary>
    public IEnumerable<string> PendingMigrations { get; set; } = new List<string>();
}
