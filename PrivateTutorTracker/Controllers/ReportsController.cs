using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateTutorTracker.Data;
using PrivateTutorTracker.Models;
using PrivateTutorTracker.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Security.Claims;

namespace PrivateTutorTracker.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IActivityLogService _activityLogService;
    private readonly CultureInfo _turkishCulture = new CultureInfo("tr-TR");
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public ReportsController(AppDbContext context, IActivityLogService activityLogService)
    {
        _context = context;
        _activityLogService = activityLogService;
        // QuestPDF lisans ayarı (Community License - ücretsiz kullanım için)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // GET: Reports
    public async Task<IActionResult> Index()
    {
        // Rapor seçim sayfası için gerekli verileri hazırla
        ViewBag.Students = await _context.Students
            .Where(s => s.AktifMi && s.UserId == CurrentUserId)
            .OrderBy(s => s.Ad)
            .ToListAsync();

        return View();
    }

    // GET: Reports/Dashboard - Gelişmiş İstatistik Dashboard
    public async Task<IActionResult> Dashboard()
    {
        try
        {
            var today = DateTime.Today;
            var thisMonthStart = new DateTime(today.Year, today.Month, 1);
            var lastMonthStart = thisMonthStart.AddMonths(-1);
            var thisYearStart = new DateTime(today.Year, 1, 1);

            // Son 12 ay için aylık istatistikler
            var monthlyStats = new List<object>();
            for (int i = 11; i >= 0; i--)
            {
                var monthStart = today.AddMonths(-i);
                monthStart = new DateTime(monthStart.Year, monthStart.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                var lessons = await _context.Lessons
                    .Where(l => l.Student!.UserId == CurrentUserId &&
                                l.Baslangic >= monthStart && l.Baslangic <= monthEnd)
                    .ToListAsync();

                monthlyStats.Add(new
                {
                    Month = monthStart.ToString("MMM yyyy", _turkishCulture),
                    TotalLessons = lessons.Count,
                    CompletedLessons = lessons.Count(l => l.Durum == "Tamamlandı"),
                    Income = lessons.Where(l => l.Durum == "Tamamlandı").Sum(l => (decimal)l.Ucret),
                    CancelledLessons = lessons.Count(l => l.Durum == "İptal")
                });
            }

            // Öğrenci bazlı istatistikler
            var studentStats = await _context.Students
                .Include(s => s.Lessons)
                .Where(s => s.AktifMi && s.UserId == CurrentUserId)
                .Select(s => new
                {
                    StudentName = s.AdSoyad,
                    TotalLessons = s.Lessons.Count,
                    CompletedLessons = s.Lessons.Count(l => l.Durum == "Tamamlandı"),
                    TotalIncome = s.Lessons.Where(l => l.Durum == "Tamamlandı").Sum(l => (decimal)l.Ucret),
                    AvgLessonFee = s.Lessons.Any() ? s.Lessons.Average(l => (decimal)l.Ucret) : 0,
                    Last30DaysLessons = s.Lessons.Count(l => l.Baslangic >= today.AddDays(-30)),
                    LastLesson = s.Lessons.Where(l => l.Baslangic <= today)
                        .Select(l => (DateTime?)l.Baslangic).Max(),
                    NextLesson = s.Lessons.Where(l => l.Baslangic > today && l.Durum == "Planlı")
                        .Select(l => (DateTime?)l.Baslangic).Min()
                })
                .OrderByDescending(s => s.TotalIncome)
                .Take(10)
                .ToListAsync();

        // Bu ay vs geçen ay karşılaştırması
        var thisMonthLessons = await _context.Lessons
            .Where(l => l.Student!.UserId == CurrentUserId && l.Baslangic >= thisMonthStart)
            .ToListAsync();

        var lastMonthLessons = await _context.Lessons
            .Where(l => l.Student!.UserId == CurrentUserId &&
                        l.Baslangic >= lastMonthStart && l.Baslangic < thisMonthStart)
            .ToListAsync();

        // Haftalık dağılım (Pazartesi-Pazar)
        var weeklyDistribution = await _context.Lessons
            .Where(l => l.Student!.UserId == CurrentUserId &&
                        l.Baslangic >= thisMonthStart && l.Durum != "İptal")
            .ToListAsync();

        var weeklyStats = new[]
        {
            new { Day = "Pazartesi", Count = weeklyDistribution.Count(l => l.Baslangic.DayOfWeek == DayOfWeek.Monday) },
            new { Day = "Salı", Count = weeklyDistribution.Count(l => l.Baslangic.DayOfWeek == DayOfWeek.Tuesday) },
            new { Day = "Çarşamba", Count = weeklyDistribution.Count(l => l.Baslangic.DayOfWeek == DayOfWeek.Wednesday) },
            new { Day = "Perşembe", Count = weeklyDistribution.Count(l => l.Baslangic.DayOfWeek == DayOfWeek.Thursday) },
            new { Day = "Cuma", Count = weeklyDistribution.Count(l => l.Baslangic.DayOfWeek == DayOfWeek.Friday) },
            new { Day = "Cumartesi", Count = weeklyDistribution.Count(l => l.Baslangic.DayOfWeek == DayOfWeek.Saturday) },
            new { Day = "Pazar", Count = weeklyDistribution.Count(l => l.Baslangic.DayOfWeek == DayOfWeek.Sunday) }
        };

        // Saatlik dağılım
        var hourlyStats = weeklyDistribution
            .GroupBy(l => l.Baslangic.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .OrderBy(h => h.Hour)
            .ToList();

            ViewBag.MonthlyStats = monthlyStats;
            ViewBag.StudentStats = studentStats;
            ViewBag.WeeklyStats = weeklyStats;
            ViewBag.HourlyStats = hourlyStats;

            ViewBag.ThisMonthIncome = thisMonthLessons.Where(l => l.Durum == "Tamamlandı").Sum(l => (decimal)l.Ucret);
            ViewBag.LastMonthIncome = lastMonthLessons.Where(l => l.Durum == "Tamamlandı").Sum(l => (decimal)l.Ucret);
            ViewBag.ThisMonthLessons = thisMonthLessons.Count;
            ViewBag.LastMonthLessons = lastMonthLessons.Count;
            ViewBag.CollectedPayments = await _context.Payments
                .Where(p => p.Student!.UserId == CurrentUserId &&
                            p.Durum == PaymentStatus.Tamamlandi && p.Tarih >= thisMonthStart)
                .SumAsync(p => (decimal?)p.Tutar) ?? 0;
            ViewBag.PendingPayments = await _context.Payments
                .Where(p => p.Student!.UserId == CurrentUserId && p.Durum == PaymentStatus.Beklemede)
                .SumAsync(p => (decimal?)p.Tutar) ?? 0;
            ViewBag.CancellationRate = thisMonthLessons.Count > 0
                ? Math.Round(thisMonthLessons.Count(l => l.Durum == "İptal") * 100m / thisMonthLessons.Count, 1)
                : 0;

            await _activityLogService.LogActivityAsync(
                "View",
                "Report",
                null,
                "Gelişmiş rapor dashboard'u görüntülendi",
                null
            );

            return View();
        }
        catch (Exception ex)
        {
            // Log the error
            await _activityLogService.LogActivityAsync(
                "Error",
                "Report",
                null,
                $"Gelişmiş rapor dashboard hatası: {ex.Message}",
                new { Error = ex.Message, StackTrace = ex.StackTrace }
            );

            TempData["ErrorMessage"] = "Rapor oluşturulurken bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
            return RedirectToAction(nameof(Index));
        }
    }

    // GET: Reports/YearlyComparison - Yıllık Karşılaştırma Raporu
    public async Task<IActionResult> YearlyComparison(int? year1, int? year2)
    {
        var y1 = year1 ?? DateTime.Today.Year;
        var y2 = year2 ?? DateTime.Today.Year - 1;

        var year1Start = new DateTime(y1, 1, 1);
        var year1End = new DateTime(y1, 12, 31);
        var year2Start = new DateTime(y2, 1, 1);
        var year2End = new DateTime(y2, 12, 31);

        var year1Lessons = await _context.Lessons
            .Include(l => l.Student)
            .Where(l => l.Student!.UserId == CurrentUserId &&
                        l.Baslangic >= year1Start && l.Baslangic <= year1End)
            .ToListAsync();

        var year2Lessons = await _context.Lessons
            .Include(l => l.Student)
            .Where(l => l.Student!.UserId == CurrentUserId &&
                        l.Baslangic >= year2Start && l.Baslangic <= year2End)
            .ToListAsync();

        // Aylık karşılaştırma
        var monthlyComparison = new List<object>();
        for (int month = 1; month <= 12; month++)
        {
            var y1MonthLessons = year1Lessons.Where(l => l.Baslangic.Month == month).ToList();
            var y2MonthLessons = year2Lessons.Where(l => l.Baslangic.Month == month).ToList();

            monthlyComparison.Add(new
            {
                Month = new DateTime(2000, month, 1).ToString("MMMM", _turkishCulture),
                Year1Count = y1MonthLessons.Count,
                Year2Count = y2MonthLessons.Count,
                Year1Income = y1MonthLessons.Where(l => l.Durum == "Tamamlandı").Sum(l => l.Ucret),
                Year2Income = y2MonthLessons.Where(l => l.Durum == "Tamamlandı").Sum(l => l.Ucret)
            });
        }

        ViewBag.Year1 = y1;
        ViewBag.Year2 = y2;
        ViewBag.MonthlyComparison = monthlyComparison;
        ViewBag.Year1TotalLessons = year1Lessons.Count;
        ViewBag.Year2TotalLessons = year2Lessons.Count;
        ViewBag.Year1TotalIncome = year1Lessons.Where(l => l.Durum == "Tamamlandı").Sum(l => l.Ucret);
        ViewBag.Year2TotalIncome = year2Lessons.Where(l => l.Durum == "Tamamlandı").Sum(l => l.Ucret);

        await _activityLogService.LogActivityAsync(
            "View",
            "Report",
            null,
            $"Yıllık karşılaştırma raporu görüntülendi: {y1} vs {y2}",
            new { Year1 = y1, Year2 = y2 }
        );

        return View();
    }

    // GET: Reports/MonthlyLessonReport
    public async Task<IActionResult> MonthlyLessonReport(int? year, int? month, string format = "pdf")
    {
        var targetDate = new DateTime(year ?? DateTime.Today.Year, month ?? DateTime.Today.Month, 1);
        var firstDayOfMonth = new DateTime(targetDate.Year, targetDate.Month, 1);
        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

        var lessons = await _context.Lessons
            .Include(l => l.Student)
            .Where(l => l.Student!.UserId == CurrentUserId &&
                        l.Baslangic >= firstDayOfMonth && l.Baslangic <= lastDayOfMonth)
            .OrderBy(l => l.Baslangic)
            .ToListAsync();

        var reportTitle = $"{targetDate:MMMM yyyy} - Aylık Ders Raporu";
        var fileName = $"AylikDersRaporu_{targetDate:yyyy_MM}";

        // Log activity
        await _activityLogService.LogActivityAsync(
            "Export",
            "Report",
            null,
            $"Aylık ders raporu oluşturuldu: {targetDate:MMMM yyyy} ({format.ToUpper()})",
            new { Year = targetDate.Year, Month = targetDate.Month, Format = format, LessonCount = lessons.Count }
        );

        if (format.ToLower() == "excel")
        {
            return GenerateMonthlyLessonExcel(lessons, reportTitle, fileName);
        }
        else
        {
            return GenerateMonthlyLessonPdf(lessons, reportTitle, fileName);
        }
    }

    // GET: Reports/StudentReport
    public async Task<IActionResult> StudentReport(int studentId, string format = "pdf")
    {
        var student = await _context.Students
            .Include(s => s.Lessons)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == studentId && s.UserId == CurrentUserId);

        if (student == null)
        {
            return NotFound();
        }

        var reportTitle = $"{student.AdSoyad} - Öğrenci Raporu";
        var fileName = $"OgrenciRaporu_{student.Ad}_{student.Soyad}_{DateTime.Now:yyyyMMdd}";

        // Log activity
        await _activityLogService.LogActivityAsync(
            "Export",
            "Report",
            studentId,
            $"Öğrenci raporu oluşturuldu: {student.AdSoyad} ({format.ToUpper()})",
            new { StudentId = studentId, StudentName = student.AdSoyad, Format = format, LessonCount = student.Lessons.Count }
        );

        if (format.ToLower() == "excel")
        {
            return GenerateStudentReportExcel(student, reportTitle, fileName);
        }
        else
        {
            return GenerateStudentReportPdf(student, reportTitle, fileName);
        }
    }

    // GET: Reports/IncomeReport
    public async Task<IActionResult> IncomeReport(int? year, int? month, string format = "pdf")
    {
        var targetDate = new DateTime(year ?? DateTime.Today.Year, month ?? DateTime.Today.Month, 1);
        var firstDayOfMonth = new DateTime(targetDate.Year, targetDate.Month, 1);
        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

        // Sadece ödeme kaydı olmayan tamamlanmış dersler
        var lessons = await _context.Lessons
            .Include(l => l.Student)
            .Where(l => l.Durum == "Tamamlandı"
                     && l.Student!.UserId == CurrentUserId
                     && l.Bitis >= firstDayOfMonth
                     && l.Bitis <= lastDayOfMonth
                     && !l.OdemeAlindi
                     && l.Ucret > 0)
            .OrderBy(l => l.Bitis)
            .ToListAsync();

        // Sadece tamamlanmış ödemeler
        var payments = await _context.Payments
            .Include(p => p.Student)
            .Where(p => p.Tarih >= firstDayOfMonth
                     && p.Student!.UserId == CurrentUserId
                     && p.Tarih <= lastDayOfMonth
                     && p.Durum == PaymentStatus.Tamamlandi)
            .OrderBy(p => p.Tarih)
            .ToListAsync();

        var reportTitle = $"{targetDate:MMMM yyyy} - Gelir Raporu";
        var fileName = $"GelirRaporu_{targetDate:yyyy_MM}";

        // Log activity
        var totalIncome = lessons.Sum(l => l.Ucret) + payments.Sum(p => p.Tutar);
        await _activityLogService.LogActivityAsync(
            "Export",
            "Report",
            null,
            $"Gelir raporu oluşturuldu: {targetDate:MMMM yyyy} ({format.ToUpper()})",
            new { Year = targetDate.Year, Month = targetDate.Month, Format = format, LessonCount = lessons.Count, PaymentCount = payments.Count, TotalIncome = totalIncome }
        );

        if (format.ToLower() == "excel")
        {
            return GenerateIncomeReportExcel(lessons, payments, targetDate, reportTitle, fileName);
        }
        else
        {
            return GenerateIncomeReportPdf(lessons, payments, targetDate, reportTitle, fileName);
        }
    }

    #region PDF Generation Methods

    private FileResult GenerateMonthlyLessonPdf(List<Models.Lesson> lessons, string title, string fileName)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header()
                    .Text(title)
                    .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(x =>
                    {
                        x.Spacing(10);

                        // Özet bilgiler
                        x.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Toplam Ders: {lessons.Count}").Bold();
                            row.RelativeItem().Text($"Tamamlanan: {lessons.Count(l => l.Durum == "Tamamlandı")}").FontColor(Colors.Green.Darken2);
                            row.RelativeItem().Text($"Planlı: {lessons.Count(l => l.Durum == "Planlı")}").FontColor(Colors.Blue.Darken1);
                            row.RelativeItem().Text($"İptal: {lessons.Count(l => l.Durum == "İptal")}").FontColor(Colors.Red.Darken1);
                        });

                        x.Item().LineHorizontal(1);

                        // Ders listesi tablosu
                        x.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80); // Tarih
                                columns.ConstantColumn(50); // Saat
                                columns.RelativeColumn(2); // Öğrenci
                                columns.RelativeColumn(2); // Konu
                                columns.ConstantColumn(70); // Durum
                                columns.ConstantColumn(60); // Ücret
                            });

                            // Başlık
                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Tarih").Bold();
                                header.Cell().Element(CellStyle).Text("Saat").Bold();
                                header.Cell().Element(CellStyle).Text("Öğrenci").Bold();
                                header.Cell().Element(CellStyle).Text("Konu").Bold();
                                header.Cell().Element(CellStyle).Text("Durum").Bold();
                                header.Cell().Element(CellStyle).Text("Ücret").Bold();

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
                                }
                            });

                            // Satırlar
                            foreach (var lesson in lessons)
                            {
                                table.Cell().Element(CellStyle).Text(lesson.Baslangic.ToString("dd.MM.yyyy"));
                                table.Cell().Element(CellStyle).Text(lesson.Baslangic.ToString("HH:mm"));
                                table.Cell().Element(CellStyle).Text(lesson.Student?.AdSoyad ?? "");
                                table.Cell().Element(CellStyle).Text(lesson.Konu ?? "");
                                table.Cell().Element(CellStyle).Text(lesson.Durum ?? "");
                                table.Cell().Element(CellStyle).Text($"{lesson.Ucret:N0}₺");

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5);
                                }
                            }
                        });

                        // Toplam gelir
                        var totalIncome = lessons.Where(l => l.Durum == "Tamamlandı").Sum(l => l.Ucret);
                        x.Item().AlignRight().Text($"Toplam Gelir: {totalIncome:N0}₺").Bold().FontSize(14).FontColor(Colors.Green.Darken2);
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Sayfa ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                        x.Span($" - {DateTime.Now:dd.MM.yyyy HH:mm}");
                    });
            });
        });

        var pdfBytes = document.GeneratePdf();
        return File(pdfBytes, "application/pdf", $"{fileName}.pdf");
    }

    private FileResult GenerateStudentReportPdf(Models.Student student, string title, string fileName)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header()
                    .Column(column =>
                    {
                        column.Item().Text(title).SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);
                        column.Item().PaddingTop(10).LineHorizontal(1);
                    });

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(x =>
                    {
                        x.Spacing(15);

                        // Öğrenci bilgileri
                        x.Item().Text("Öğrenci Bilgileri").Bold().FontSize(14);
                        x.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                            });

                            AddInfoRow(table, "Ad Soyad:", student.AdSoyad ?? "");
                            AddInfoRow(table, "Email:", student.Email ?? "");
                            AddInfoRow(table, "Telefon:", student.Telefon ?? "");

                            static void AddInfoRow(TableDescriptor table, string label, string value)
                            {
                                table.Cell().Padding(5).Text(label).Bold();
                                table.Cell().Padding(5).Text(value);
                            }
                        });

                        // İstatistikler
                        x.Item().PaddingTop(10).Text("Ders İstatistikleri").Bold().FontSize(14);
                        var completedLessons = student.Lessons.Where(l => l.Durum == "Tamamlandı").ToList();
                        var totalIncome = completedLessons.Sum(l => l.Ucret);
                        var paidLessons = student.Lessons.Count(l => l.OdemeAlindi);

                        x.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Toplam Ders: {student.Lessons.Count}");
                            row.RelativeItem().Text($"Tamamlanan: {completedLessons.Count}");
                            row.RelativeItem().Text($"Gelir: {totalIncome:N0}₺");
                        });

                        var lastLesson = student.Lessons.Where(l => l.Baslangic <= DateTime.Now)
                            .OrderByDescending(l => l.Baslangic).FirstOrDefault();
                        var nextLesson = student.Lessons.Where(l => l.Baslangic > DateTime.Now && l.Durum == "Planlı")
                            .OrderBy(l => l.Baslangic).FirstOrDefault();
                        var pendingAmount = student.Payments.Where(p => p.Durum == PaymentStatus.Beklemede).Sum(p => p.Tutar);
                        x.Item().Background(Colors.Grey.Lighten4).Padding(8).Row(row =>
                        {
                            row.RelativeItem().Text($"Son ders: {(lastLesson == null ? "Yok" : lastLesson.Baslangic.ToString("dd.MM.yyyy"))}");
                            row.RelativeItem().Text($"Sonraki ders: {(nextLesson == null ? "Planlanmadı" : nextLesson.Baslangic.ToString("dd.MM.yyyy HH:mm"))}");
                            row.RelativeItem().Text($"Bekleyen ödeme: {pendingAmount:N0}₺");
                        });

                        // Dersler
                        x.Item().PaddingTop(10).Text("Ders Geçmişi").Bold().FontSize(14);
                        x.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80);
                                columns.ConstantColumn(50);
                                columns.RelativeColumn(2);
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(60);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Tarih").Bold();
                                header.Cell().Element(CellStyle).Text("Saat").Bold();
                                header.Cell().Element(CellStyle).Text("Konu").Bold();
                                header.Cell().Element(CellStyle).Text("Durum").Bold();
                                header.Cell().Element(CellStyle).Text("Ücret").Bold();

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
                                }
                            });

                            foreach (var lesson in student.Lessons.OrderByDescending(l => l.Baslangic).Take(20))
                            {
                                table.Cell().Element(CellStyle).Text(lesson.Baslangic.ToString("dd.MM.yyyy"));
                                table.Cell().Element(CellStyle).Text(lesson.Baslangic.ToString("HH:mm"));
                                table.Cell().Element(CellStyle).Text(lesson.Konu ?? "");
                                table.Cell().Element(CellStyle).Text(lesson.Durum ?? "");
                                table.Cell().Element(CellStyle).Text($"{lesson.Ucret:N0}₺");

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5);
                                }
                            }
                        });

                        if (student.Lessons.Count > 20)
                        {
                            x.Item().Text($"(Toplam {student.Lessons.Count} ders, son 20 ders gösteriliyor)").FontSize(9).Italic();
                        }
                    });

                page.Footer()
                    .AlignCenter()
                    .Text($"{DateTime.Now:dd.MM.yyyy HH:mm}");
            });
        });

        var pdfBytes = document.GeneratePdf();
        return File(pdfBytes, "application/pdf", $"{fileName}.pdf");
    }

    private FileResult GenerateIncomeReportPdf(List<Models.Lesson> lessons, List<Models.Payment> payments,
        DateTime targetDate, string title, string fileName)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header()
                    .Text(title)
                    .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(x =>
                    {
                        x.Spacing(15);

                        // Özet
                        var lessonIncome = lessons.Sum(l => l.Ucret);
                        var paymentIncome = payments.Sum(p => p.Tutar);
                        var totalIncome = lessonIncome + paymentIncome;

                        x.Item().Background(Colors.Blue.Lighten4).Padding(10).Column(col =>
                        {
                            col.Item().Text("Gelir Özeti").Bold().FontSize(14);
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"Ödeme Kaydı Olmayan Dersler: {lessonIncome:N0}₺");
                                row.RelativeItem().Text($"Alınan Ödemeler: {paymentIncome:N0}₺");
                                row.RelativeItem().Text($"TOPLAM GELİR: {totalIncome:N0}₺").Bold().FontSize(12);
                            });
                        });

                        // Dersler
                        x.Item().Text("Ödeme Kaydı Olmayan Tamamlanmış Dersler").Bold().FontSize(14);
                        x.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.ConstantColumn(60);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Tarih").Bold();
                                header.Cell().Element(CellStyle).Text("Öğrenci").Bold();
                                header.Cell().Element(CellStyle).Text("Konu").Bold();
                                header.Cell().Element(CellStyle).Text("Ücret").Bold();

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
                                }
                            });

                            foreach (var lesson in lessons)
                            {
                                table.Cell().Element(CellStyle).Text(lesson.Bitis.ToString("dd.MM.yyyy"));
                                table.Cell().Element(CellStyle).Text(lesson.Student?.AdSoyad ?? "");
                                table.Cell().Element(CellStyle).Text(lesson.Konu ?? "");
                                table.Cell().Element(CellStyle).Text($"{lesson.Ucret:N0}₺");

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5);
                                }
                            }
                        });

                        // Ödemeler
                        if (payments.Any())
                        {
                            x.Item().PaddingTop(10).Text("Alınan Ödemeler").Bold().FontSize(14);
                            x.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(80);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.ConstantColumn(60);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("Tarih").Bold();
                                    header.Cell().Element(CellStyle).Text("Öğrenci").Bold();
                                    header.Cell().Element(CellStyle).Text("Açıklama").Bold();
                                    header.Cell().Element(CellStyle).Text("Tutar").Bold();

                                    static IContainer CellStyle(IContainer container)
                                    {
                                        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
                                    }
                                });

                                foreach (var payment in payments)
                                {
                                    table.Cell().Element(CellStyle).Text(payment.Tarih.ToString("dd.MM.yyyy"));
                                    table.Cell().Element(CellStyle).Text(payment.Student?.AdSoyad ?? "");
                                    table.Cell().Element(CellStyle).Text(payment.Aciklama ?? "");
                                    table.Cell().Element(CellStyle).Text($"{payment.Tutar:N0}₺");

                                    static IContainer CellStyle(IContainer container)
                                    {
                                        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5);
                                    }
                                }
                            });
                        }
                    });

                page.Footer()
                    .AlignCenter()
                    .Text($"{DateTime.Now:dd.MM.yyyy HH:mm}");
            });
        });

        var pdfBytes = document.GeneratePdf();
        return File(pdfBytes, "application/pdf", $"{fileName}.pdf");
    }

    #endregion

    #region Excel Generation Methods

    private FileResult GenerateMonthlyLessonExcel(List<Models.Lesson> lessons, string title, string fileName)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Dersler");

        // Başlık
        worksheet.Cell(1, 1).Value = title;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Range(1, 1, 1, 6).Merge();

        // Özet bilgiler
        worksheet.Cell(2, 1).Value = "Toplam Ders:";
        worksheet.Cell(2, 2).Value = lessons.Count;
        worksheet.Cell(2, 3).Value = "Tamamlanan:";
        worksheet.Cell(2, 4).Value = lessons.Count(l => l.Durum == "Tamamlandı");
        worksheet.Cell(2, 5).Value = "Planlı:";
        worksheet.Cell(2, 6).Value = lessons.Count(l => l.Durum == "Planlı");

        // Tablo başlıkları
        var headerRow = 4;
        worksheet.Cell(headerRow, 1).Value = "Tarih";
        worksheet.Cell(headerRow, 2).Value = "Saat";
        worksheet.Cell(headerRow, 3).Value = "Öğrenci";
        worksheet.Cell(headerRow, 4).Value = "Konu";
        worksheet.Cell(headerRow, 5).Value = "Durum";
        worksheet.Cell(headerRow, 6).Value = "Ücret (₺)";

        worksheet.Range(headerRow, 1, headerRow, 6).Style.Font.Bold = true;
        worksheet.Range(headerRow, 1, headerRow, 6).Style.Fill.BackgroundColor = XLColor.LightBlue;

        // Veriler
        var row = headerRow + 1;
        foreach (var lesson in lessons)
        {
            worksheet.Cell(row, 1).Value = lesson.Baslangic.ToString("dd.MM.yyyy");
            worksheet.Cell(row, 2).Value = lesson.Baslangic.ToString("HH:mm");
            worksheet.Cell(row, 3).Value = lesson.Student?.AdSoyad ?? "";
            worksheet.Cell(row, 4).Value = lesson.Konu ?? "";
            worksheet.Cell(row, 5).Value = lesson.Durum ?? "";
            worksheet.Cell(row, 6).Value = lesson.Ucret;
            row++;
        }

        // Toplam
        worksheet.Cell(row + 1, 5).Value = "Toplam Gelir:";
        worksheet.Cell(row + 1, 5).Style.Font.Bold = true;
        worksheet.Cell(row + 1, 6).Value = lessons.Where(l => l.Durum == "Tamamlandı").Sum(l => l.Ucret);
        worksheet.Cell(row + 1, 6).Style.Font.Bold = true;
        worksheet.Cell(row + 1, 6).Style.Fill.BackgroundColor = XLColor.LightGreen;

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileName}.xlsx");
    }

    private FileResult GenerateStudentReportExcel(Models.Student student, string title, string fileName)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Öğrenci Raporu");

        // Başlık
        worksheet.Cell(1, 1).Value = title;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Range(1, 1, 1, 5).Merge();

        // Öğrenci bilgileri
        var row = 3;
        worksheet.Cell(row, 1).Value = "Ad Soyad:";
        worksheet.Cell(row, 2).Value = student.AdSoyad;
        row++;
        worksheet.Cell(row, 1).Value = "Email:";
        worksheet.Cell(row, 2).Value = student.Email;
        row++;
        worksheet.Cell(row, 1).Value = "Telefon:";
        worksheet.Cell(row, 2).Value = student.Telefon;
        row += 2;

        // İstatistikler
        var completedLessons = student.Lessons.Where(l => l.Durum == "Tamamlandı").ToList();
        var totalIncome = completedLessons.Sum(l => l.Ucret);

        worksheet.Cell(row, 1).Value = "Toplam Ders:";
        worksheet.Cell(row, 2).Value = student.Lessons.Count;
        worksheet.Cell(row, 3).Value = "Tamamlanan:";
        worksheet.Cell(row, 4).Value = completedLessons.Count;
        worksheet.Cell(row, 5).Value = $"Toplam Gelir: {totalIncome:N0}₺";
        row += 2;

        // Dersler tablosu
        worksheet.Cell(row, 1).Value = "Tarih";
        worksheet.Cell(row, 2).Value = "Saat";
        worksheet.Cell(row, 3).Value = "Konu";
        worksheet.Cell(row, 4).Value = "Durum";
        worksheet.Cell(row, 5).Value = "Ücret (₺)";
        worksheet.Range(row, 1, row, 5).Style.Font.Bold = true;
        worksheet.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.LightBlue;
        row++;

        foreach (var lesson in student.Lessons.OrderByDescending(l => l.Baslangic))
        {
            worksheet.Cell(row, 1).Value = lesson.Baslangic.ToString("dd.MM.yyyy");
            worksheet.Cell(row, 2).Value = lesson.Baslangic.ToString("HH:mm");
            worksheet.Cell(row, 3).Value = lesson.Konu ?? "";
            worksheet.Cell(row, 4).Value = lesson.Durum ?? "";
            worksheet.Cell(row, 5).Value = lesson.Ucret;
            row++;
        }

        worksheet.Columns().AdjustToContents();

        var paymentsSheet = workbook.Worksheets.Add("Ödemeler");
        paymentsSheet.Cell(1, 1).Value = $"{student.AdSoyad} - Ödeme Geçmişi";
        paymentsSheet.Cell(1, 1).Style.Font.Bold = true;
        paymentsSheet.Cell(1, 1).Style.Font.FontSize = 16;
        paymentsSheet.Range(1, 1, 1, 5).Merge();
        paymentsSheet.Cell(3, 1).Value = "Tarih";
        paymentsSheet.Cell(3, 2).Value = "Tutar (₺)";
        paymentsSheet.Cell(3, 3).Value = "Dönem";
        paymentsSheet.Cell(3, 4).Value = "Durum";
        paymentsSheet.Cell(3, 5).Value = "Açıklama";
        paymentsSheet.Range(3, 1, 3, 5).Style.Font.Bold = true;
        paymentsSheet.Range(3, 1, 3, 5).Style.Fill.BackgroundColor = XLColor.LightGreen;

        var paymentRow = 4;
        foreach (var payment in student.Payments.OrderByDescending(p => p.Tarih))
        {
            paymentsSheet.Cell(paymentRow, 1).Value = payment.Tarih;
            paymentsSheet.Cell(paymentRow, 1).Style.DateFormat.Format = "dd.MM.yyyy";
            paymentsSheet.Cell(paymentRow, 2).Value = payment.Tutar;
            paymentsSheet.Cell(paymentRow, 2).Style.NumberFormat.Format = "#,##0.00";
            paymentsSheet.Cell(paymentRow, 3).Value = payment.Donem;
            paymentsSheet.Cell(paymentRow, 4).Value = payment.Durum.ToString();
            paymentsSheet.Cell(paymentRow, 5).Value = payment.Aciklama ?? string.Empty;
            paymentRow++;
        }

        paymentsSheet.Cell(paymentRow + 1, 1).Value = "Tamamlanan ödeme toplamı";
        paymentsSheet.Cell(paymentRow + 1, 1).Style.Font.Bold = true;
        paymentsSheet.Cell(paymentRow + 1, 2).Value = student.Payments
            .Where(p => p.Durum == PaymentStatus.Tamamlandi).Sum(p => p.Tutar);
        paymentsSheet.Cell(paymentRow + 1, 2).Style.Font.Bold = true;
        paymentsSheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileName}.xlsx");
    }

    private FileResult GenerateIncomeReportExcel(List<Models.Lesson> lessons, List<Models.Payment> payments,
        DateTime targetDate, string title, string fileName)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Gelir Raporu");

        // Başlık
        worksheet.Cell(1, 1).Value = title;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Range(1, 1, 1, 4).Merge();

        // Özet
        var lessonIncome = lessons.Sum(l => l.Ucret);
        var paymentIncome = payments.Sum(p => p.Tutar);
        var totalIncome = lessonIncome + paymentIncome;

        var row = 3;
        worksheet.Cell(row, 1).Value = "Ödeme Kaydı Olmayan Dersler:";
        worksheet.Cell(row, 2).Value = $"{lessonIncome:N0}₺";
        row++;
        worksheet.Cell(row, 1).Value = "Alınan Ödemeler:";
        worksheet.Cell(row, 2).Value = $"{paymentIncome:N0}₺";
        row++;
        worksheet.Cell(row, 1).Value = "TOPLAM GELİR:";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 2).Value = $"{totalIncome:N0}₺";
        worksheet.Cell(row, 2).Style.Font.Bold = true;
        worksheet.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.LightGreen;
        row += 2;

        // Dersler
        worksheet.Cell(row, 1).Value = "Ödeme Kaydı Olmayan Tamamlanmış Dersler";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        worksheet.Cell(row, 1).Value = "Tarih";
        worksheet.Cell(row, 2).Value = "Öğrenci";
        worksheet.Cell(row, 3).Value = "Konu";
        worksheet.Cell(row, 4).Value = "Ücret (₺)";
        worksheet.Range(row, 1, row, 4).Style.Font.Bold = true;
        worksheet.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.LightBlue;
        row++;

        foreach (var lesson in lessons)
        {
            worksheet.Cell(row, 1).Value = lesson.Bitis.ToString("dd.MM.yyyy");
            worksheet.Cell(row, 2).Value = lesson.Student?.AdSoyad ?? "";
            worksheet.Cell(row, 3).Value = lesson.Konu ?? "";
            worksheet.Cell(row, 4).Value = lesson.Ucret;
            row++;
        }

        row += 2;

        // Ödemeler
        if (payments.Any())
        {
            worksheet.Cell(row, 1).Value = "Alınan Ödemeler";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            row++;

            worksheet.Cell(row, 1).Value = "Tarih";
            worksheet.Cell(row, 2).Value = "Öğrenci";
            worksheet.Cell(row, 3).Value = "Açıklama";
            worksheet.Cell(row, 4).Value = "Tutar (₺)";
            worksheet.Range(row, 1, row, 4).Style.Font.Bold = true;
            worksheet.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.LightBlue;
            row++;

            foreach (var payment in payments)
            {
                worksheet.Cell(row, 1).Value = payment.Tarih.ToString("dd.MM.yyyy");
                worksheet.Cell(row, 2).Value = payment.Student?.AdSoyad ?? "";
                worksheet.Cell(row, 3).Value = payment.Aciklama ?? "";
                worksheet.Cell(row, 4).Value = payment.Tutar;
                row++;
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileName}.xlsx");
    }

    #endregion
}
