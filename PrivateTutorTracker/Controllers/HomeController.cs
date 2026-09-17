using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateTutorTracker.Data;
using PrivateTutorTracker.Models;

namespace PrivateTutorTracker.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(ILogger<HomeController> logger, AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id ?? string.Empty;
    }

    public async Task<IActionResult> Index()
    {
        var userId = await GetCurrentUserIdAsync();

        // Dashboard istatistikleri
        var currentMonth = DateTime.Now.Month;
        var currentYear = DateTime.Now.Year;

        // Bu ay tamamlanan dersler (sadece kullanıcının öğrencileri)
        var monthlyLessons = await _context.Lessons
            .Include(l => l.Student)
            .Where(l => l.Durum == "Tamamlandı"
                     && l.Bitis.Month == currentMonth
                     && l.Bitis.Year == currentYear
                     && l.Student!.UserId == userId)
            .ToListAsync();

        ViewBag.MonthlyLessonCount = monthlyLessons.Count;

        // Bu ay tamamlanan derslerden sadece ödeme kaydı OLMAYANLARIN ücreti
        var monthlyLessonIncomeWithoutPayment = monthlyLessons
            .Where(l => !l.OdemeAlindi && l.Ucret > 0)
            .Sum(l => l.Ucret);

        ViewBag.MonthlyIncome = monthlyLessonIncomeWithoutPayment;

        // Bu ay alınan ödemeler (sadece kullanıcının öğrencileri)
        var monthlyPayments = await _context.Payments
            .Include(p => p.Student)
            .Where(p => p.Tarih.Month == currentMonth
                     && p.Tarih.Year == currentYear
                     && p.Durum == PaymentStatus.Tamamlandi
                     && p.Student!.UserId == userId)
            .ToListAsync();

        ViewBag.MonthlyPaymentTotal = monthlyPayments.Sum(p => p.Tutar);
        ViewBag.MonthlyPaymentCount = monthlyPayments.Count;

        // Aktif öğrenci sayısı (sadece kullanıcının öğrencileri)
        ViewBag.ActiveStudentCount = await _context.Students.CountAsync(s => s.AktifMi && s.UserId == userId);

        // Bu hafta planlanan dersler (sadece kullanıcının öğrencileri)
        var today = DateTime.Today;
        var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
        var endOfWeek = startOfWeek.AddDays(7);

        ViewBag.WeeklyPlannedLessons = await _context.Lessons
            .Include(l => l.Student)
            .Where(l => l.Durum == "Planlı"
                     && l.Baslangic >= startOfWeek
                     && l.Baslangic < endOfWeek
                     && l.Student!.UserId == userId)
            .CountAsync();

        // Son dersler (sadece kullanıcının öğrencileri)
        var recentLessons = await _context.Lessons
            .Include(l => l.Student)
            .Where(l => l.Student!.UserId == userId)
            .OrderByDescending(l => l.Baslangic)
            .Take(5)
            .ToListAsync();

        ViewBag.RecentLessons = recentLessons;

        // Öğrenci bazlı istatistikler (sadece kullanıcının öğrencileri)
        var studentStats = await _context.Students
            .Where(s => s.AktifMi && s.UserId == userId)
            .Select(s => new
            {
                Student = s,
                TotalLessons = s.Lessons.Count(l => l.Durum == "Tamamlandı"),
                TotalIncome = s.Lessons.Where(l => l.Durum == "Tamamlandı").Sum(l => l.Ucret)
            })
            .OrderByDescending(x => x.TotalLessons)
            .Take(5)
            .ToListAsync();

        ViewBag.StudentStats = studentStats;

        // Tamamlanan derslerin oranı (%)
        var totalLessons = await _context.Lessons.CountAsync(l => l.Student!.UserId == userId);
        var completedLessons = await _context.Lessons.CountAsync(l =>
            l.Durum == "Tamamlandı" && l.Student!.UserId == userId);
        ViewBag.CompletionRate = totalLessons > 0 ? (int)((completedLessons / (double)totalLessons) * 100) : 0;

        // Bu ay toplam gelir (Sadece ödeme kaydı olmayanlar + Alınan ödemeler)
        ViewBag.MonthlyTotalIncome = monthlyLessonIncomeWithoutPayment + monthlyPayments.Sum(p => p.Tutar);

        // En aktif gün (En çok ders yapılan gün)
        var completedLessonsForDays = await _context.Lessons
            .Where(l => l.Durum == "Tamamlandı" && l.Student!.UserId == userId)
            .Select(l => l.Baslangic)
            .ToListAsync();

        var lessonsByDay = completedLessonsForDays
            .GroupBy(date => date.DayOfWeek)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .FirstOrDefault();

        ViewBag.MostActiveDay = lessonsByDay != null
            ? GetDayNameInTurkish(lessonsByDay.Day)
            : "Veri yok";
        ViewBag.MostActiveDayCount = lessonsByDay?.Count ?? 0;

        // Toplam ders süresi (dakika → saat/dk formatında)
        var completedLessonsForDuration = await _context.Lessons
            .Where(l => l.Durum == "Tamamlandı" && l.Student!.UserId == userId)
            .Select(l => new { l.Baslangic, l.Bitis })
            .ToListAsync();

        var totalMinutes = completedLessonsForDuration
            .Sum(l => (int)(l.Bitis - l.Baslangic).TotalMinutes);

        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        ViewBag.TotalDurationHours = hours;
        ViewBag.TotalDurationMinutes = minutes;
        ViewBag.TotalDurationFormatted = $"{hours}s {minutes}dk";

        // ===== CHART.JS GRAFİK VERİLERİ =====

        // 1. Aylık Gelir Grafiği (Son 6 ay)
        var last6Months = Enumerable.Range(0, 6)
            .Select(i => DateTime.Now.AddMonths(-5 + i))
            .ToList();

        var monthlyIncomeData = new List<decimal>();
        var monthLabels = new List<string>();

        foreach (var month in last6Months)
        {
            // Sadece ödeme kaydı olmayan tamamlanmış derslerin geliri
            var lessonsWithoutPayment = await _context.Lessons
                .Where(l => l.Durum == "Tamamlandı"
                         && l.Student!.UserId == userId
                         && l.Bitis.Month == month.Month
                         && l.Bitis.Year == month.Year
                         && !l.OdemeAlindi
                         && l.Ucret > 0)
                .SumAsync(l => l.Ucret);

            // Sadece tamamlanmış ödemelerin geliri
            var paymentsIncome = await _context.Payments
                .Where(p => p.Tarih.Month == month.Month
                         && p.Student!.UserId == userId
                         && p.Tarih.Year == month.Year
                         && p.Durum == PaymentStatus.Tamamlandi)
                .SumAsync(p => p.Tutar);

            monthlyIncomeData.Add(lessonsWithoutPayment + paymentsIncome);
            monthLabels.Add(month.ToString("MMMM"));
        }

        ViewBag.MonthlyIncomeData = System.Text.Json.JsonSerializer.Serialize(monthlyIncomeData);
        ViewBag.MonthLabels = System.Text.Json.JsonSerializer.Serialize(monthLabels);

        // 2. Ders Durum Dağılımı (Pie Chart)
        var plannedCount = await _context.Lessons.CountAsync(l => l.Durum == "Planlı" && l.Student!.UserId == userId);
        var completedCount = await _context.Lessons.CountAsync(l => l.Durum == "Tamamlandı" && l.Student!.UserId == userId);
        var cancelledCount = await _context.Lessons.CountAsync(l => l.Durum == "İptal" && l.Student!.UserId == userId);

        ViewBag.LessonStatusData = System.Text.Json.JsonSerializer.Serialize(new[] { plannedCount, completedCount, cancelledCount });

        // 3. Haftalık Ders Yoğunluğu (Bar Chart - Son 7 gün)
        var last7Days = Enumerable.Range(0, 7)
            .Select(i => DateTime.Today.AddDays(-6 + i))
            .ToList();

        var dailyLessonCounts = new List<int>();
        var dayLabels = new List<string>();

        foreach (var day in last7Days)
        {
            var count = await _context.Lessons
                .Where(l => l.Student!.UserId == userId && l.Baslangic.Date == day.Date)
                .CountAsync();

            dailyLessonCounts.Add(count);
            dayLabels.Add(day.ToString("dd MMM"));
        }

        ViewBag.DailyLessonData = System.Text.Json.JsonSerializer.Serialize(dailyLessonCounts);
        ViewBag.DayLabels = System.Text.Json.JsonSerializer.Serialize(dayLabels);

        return View();
    }

    private string GetDayNameInTurkish(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Monday => "Pazartesi",
            DayOfWeek.Tuesday => "Salı",
            DayOfWeek.Wednesday => "Çarşamba",
            DayOfWeek.Thursday => "Perşembe",
            DayOfWeek.Friday => "Cuma",
            DayOfWeek.Saturday => "Cumartesi",
            DayOfWeek.Sunday => "Pazar",
            _ => "Bilinmiyor"
        };
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
