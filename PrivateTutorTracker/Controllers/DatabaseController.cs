using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateTutorTracker.Data;
using PrivateTutorTracker.Models;

namespace PrivateTutorTracker.Controllers;

[Authorize]
public class DatabaseController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<DatabaseController> _logger;
    private readonly IWebHostEnvironment _environment;

    public DatabaseController(
        AppDbContext context,
        ILogger<DatabaseController> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _logger = logger;
        _environment = environment;
    }

    // Database Migration sayfası
    [HttpGet]
    public IActionResult Migrate()
    {
        // Sadece Development ortamında erişilebilir
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        ViewBag.Message = TempData["Message"];
        ViewBag.Error = TempData["Error"];

        return View();
    }

    // Migration'ları uygula
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyMigrations()
    {
        // Sadece Development ortamında erişilebilir
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            _logger.LogInformation("Veritabanı migration'ları uygulanıyor...");

            // Bekleyen migration'ları al
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
            var pendingList = pendingMigrations.ToList();

            if (!pendingList.Any())
            {
                TempData["Message"] = "Uygulanacak bekleyen migration yok.";
                _logger.LogInformation("Bekleyen migration yok.");
            }
            else
            {
                // Migration'ları uygula
                await _context.Database.MigrateAsync();

                TempData["Message"] = $"✅ {pendingList.Count} migration başarıyla uygulandı!";
                _logger.LogInformation($"{pendingList.Count} migration başarıyla uygulandı.");
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"❌ Hata: {ex.Message}";
            _logger.LogError(ex, "Migration uygulanırken hata oluştu");
        }

        return RedirectToAction(nameof(Migrate));
    }

    // Veritabanı bilgilerini göster
    [HttpGet]
    public async Task<IActionResult> Info()
    {
        // Sadece Development ortamında erişilebilir
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            var model = new DatabaseInfoViewModel
            {
                CanConnect = await _context.Database.CanConnectAsync(),
                ConnectionString = _context.Database.GetConnectionString(),
                AppliedMigrations = await _context.Database.GetAppliedMigrationsAsync(),
                PendingMigrations = await _context.Database.GetPendingMigrationsAsync()
            };

            return View(model);
        }
        catch (Exception ex)
        {
            ViewBag.Error = $"Veritabanı bilgileri alınamadı: {ex.Message}";
            return View(new DatabaseInfoViewModel());
        }
    }
}
