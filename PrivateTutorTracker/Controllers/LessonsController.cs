using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PrivateTutorTracker.Data;
using PrivateTutorTracker.Models;
using PrivateTutorTracker.Services;

namespace PrivateTutorTracker.Controllers;

[Authorize]
public class LessonsController : Controller
{
    private readonly AppDbContext _context;
    private readonly GoogleCalendarService _calendarService;
    private readonly ZoomMeetingService _zoomMeetingService;
    private readonly IActivityLogService _activityLogService;
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly decimal _defaultLessonPrice;

    public LessonsController(
        AppDbContext context,
        GoogleCalendarService calendarService,
        ZoomMeetingService zoomMeetingService,
        IActivityLogService activityLogService,
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _calendarService = calendarService;
        _zoomMeetingService = zoomMeetingService;
        _activityLogService = activityLogService;
        _configuration = configuration;
        _userManager = userManager;
        _defaultLessonPrice = _configuration.GetValue<decimal>("LessonSettings:DefaultLessonPrice", 450m);
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id ?? string.Empty;
    }

    // GET: Lessons
    public async Task<IActionResult> Index(string durum = "", int? studentId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var userId = await GetCurrentUserIdAsync();

        // Sadece mevcut kullanıcının öğrencilerine ait dersleri göster
        var query = _context.Lessons
            .Include(l => l.Student)
            .Where(l => l.Student!.UserId == userId)
            .AsQueryable();

        // Filtreler
        if (!string.IsNullOrEmpty(durum))
        {
            query = query.Where(l => l.Durum == durum);
        }

        if (studentId.HasValue)
        {
            query = query.Where(l => l.StudentId == studentId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(l => l.Baslangic >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(l => l.Baslangic <= endDate.Value);
        }

        var lessons = await query
            .OrderByDescending(l => l.Baslangic)
            .ToListAsync();

        // ViewBag için öğrenci listesi
        ViewBag.Students = await _context.Students
            .Where(s => s.AktifMi)
            .OrderBy(s => s.Ad)
            .ToListAsync();

        ViewBag.CurrentDurum = durum;
        ViewBag.CurrentStudentId = studentId;
        ViewBag.CurrentStartDate = startDate;
        ViewBag.CurrentEndDate = endDate;

        return View(lessons);
    }

    // GET: Lessons/Calendar
    public async Task<IActionResult> Calendar(DateTime? date)
    {
        var targetDate = date ?? DateTime.Today;
        var startOfWeek = targetDate.AddDays(-(int)targetDate.DayOfWeek + (int)DayOfWeek.Monday);
        var endOfWeek = startOfWeek.AddDays(7);

        var lessons = await _context.Lessons
            .Include(l => l.Student)
            .Where(l => l.Baslangic >= startOfWeek && l.Baslangic < endOfWeek)
            .OrderBy(l => l.Baslangic)
            .ToListAsync();

        ViewBag.TargetDate = targetDate;
        ViewBag.StartOfWeek = startOfWeek;
        ViewBag.EndOfWeek = endOfWeek;
        ViewBag.Students = await _context.Students.Where(s => s.AktifMi).ToListAsync();

        return View(lessons);
    }

    // GET: Lessons/MonthlyCalendar
    public async Task<IActionResult> MonthlyCalendar(int? year, int? month)
    {
        var targetDate = new DateTime(year ?? DateTime.Today.Year, month ?? DateTime.Today.Month, 1);
        var firstDayOfMonth = new DateTime(targetDate.Year, targetDate.Month, 1);
        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

        // Takvim görünümü için ayın ilk haftasının pazartesinden başlayalım
        var startOfCalendar = firstDayOfMonth.AddDays(-(int)firstDayOfMonth.DayOfWeek + (int)DayOfWeek.Monday);
        if (startOfCalendar > firstDayOfMonth)
            startOfCalendar = startOfCalendar.AddDays(-7);

        // Takvim görünümü için ayın son haftasının pazarına kadar gidelim
        var endOfCalendar = lastDayOfMonth.AddDays(7 - (int)lastDayOfMonth.DayOfWeek);
        if (endOfCalendar < lastDayOfMonth)
            endOfCalendar = endOfCalendar.AddDays(7);

        var lessons = await _context.Lessons
            .Include(l => l.Student)
            .Where(l => l.Baslangic >= startOfCalendar && l.Baslangic <= endOfCalendar)
            .OrderBy(l => l.Baslangic)
            .ToListAsync();

        ViewBag.TargetDate = targetDate;
        ViewBag.FirstDayOfMonth = firstDayOfMonth;
        ViewBag.LastDayOfMonth = lastDayOfMonth;
        ViewBag.StartOfCalendar = startOfCalendar;
        ViewBag.EndOfCalendar = endOfCalendar;
        ViewBag.Students = await _context.Students.Where(s => s.AktifMi).ToListAsync();

        return View(lessons);
    }

    // GET: Lessons/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var lesson = await _context.Lessons
            .Include(l => l.Student)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (lesson == null)
        {
            return NotFound();
        }

        return View(lesson);
    }

    // GET: Lessons/Create
    public async Task<IActionResult> Create(DateTime? baslangic, int? studentId)
    {
        // Eğer studentId parametresi geldiyse öğrenciyi pre-select yap
        await PopulateStudentsDropDown(studentId);

        // Ders şablonlarını ViewBag'e ekle
        var templates = await _context.LessonTemplates
            .Where(t => t.AktifMi)
            .OrderBy(t => t.Ad)
            .ToListAsync();
        ViewBag.Templates = templates;

        // Varsayılan ders ücretini kullan (appsettings.json'dan)
        var lesson = new Lesson
        {
            StudentId = studentId ?? 0,
            Baslangic = baslangic ?? DateTime.Now,
            Bitis = (baslangic ?? DateTime.Now).AddMinutes(40),
            Ucret = _defaultLessonPrice
        };

        return View(lesson);
    }

    // POST: Lessons/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("StudentId,Baslangic,Bitis,Konu,Ucret,Durum,Not,OdemeAlindi,Tur,DersNotu")] Lesson lesson, int DersSayisi = 1, bool TekrarliMi = false, string? TekrarPeriyodu = null, int TekrarSayisi = 1)
    {
        var allowedLessonFees = new[] { 450m, 500m, 550m, 600m, 650m, 700m, 750m };
        if (!allowedLessonFees.Contains(lesson.Ucret))
        {
            ModelState.AddModelError(nameof(Lesson.Ucret), "Ders ücreti 450 ₺ ile 750 ₺ arasında 50 ₺ adımlarla seçilmelidir.");
        }

        // DEBUG: Gelen değerleri logla
        Console.WriteLine("=== CREATE POST İSTEĞİ ===");
        Console.WriteLine($"StudentId: {lesson.StudentId}");
        Console.WriteLine($"Baslangic: {lesson.Baslangic}");
        Console.WriteLine($"Bitis: {lesson.Bitis}");
        Console.WriteLine($"Konu: {lesson.Konu}");
        Console.WriteLine($"Ucret: {lesson.Ucret}");
        Console.WriteLine($"Durum: {lesson.Durum}");
        Console.WriteLine($"DersSayisi: {DersSayisi}");
        Console.WriteLine($"TekrarliMi: {TekrarliMi}");
        Console.WriteLine($"TekrarPeriyodu: {TekrarPeriyodu}");
        Console.WriteLine($"TekrarSayisi: {TekrarSayisi}");

        // Request.Form değerlerini kontrol et
        Console.WriteLine("\n=== FORM VERİLERİ ===");
        foreach (var key in Request.Form.Keys)
        {
            Console.WriteLine($"{key}: {Request.Form[key]}");
        }

        // ModelState hatalarını logla
        if (!ModelState.IsValid)
        {
            Console.WriteLine("\n=== MODELSTATE HATALARI ===");
            foreach (var key in ModelState.Keys)
            {
                var errors = ModelState[key]?.Errors;
                if (errors != null && errors.Count > 0)
                {
                    Console.WriteLine($"Alan: {key}");
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"  - Hata: {error.ErrorMessage}");
                        if (error.Exception != null)
                        {
                            Console.WriteLine($"  - Exception: {error.Exception.Message}");
                        }
                    }
                }
            }
        }

        if (ModelState.IsValid)
        {
            Console.WriteLine("✅ ModelState geçerli, kayıt işlemi başlatılıyor...");
            var lessonsToAdd = new List<Lesson>();
            var hasConflict = false;
            var conflictMessages = new List<string>();

            // Generate unique group ID for recurring lessons
            int? tekrarGrupId = null;
            if (TekrarliMi && !string.IsNullOrEmpty(TekrarPeriyodu))
            {
                tekrarGrupId = new Random().Next(100000, 999999);
            }

            // Determine how many recurrences
            int actualTekrarSayisi = (TekrarliMi && !string.IsNullOrEmpty(TekrarPeriyodu)) ? TekrarSayisi : 1;

            // Determine interval in days based on period
            int intervalDays = 0;
            if (TekrarliMi && !string.IsNullOrEmpty(TekrarPeriyodu))
            {
                intervalDays = TekrarPeriyodu switch
                {
                    "Günlük" => 1,
                    "Haftalık" => 7,
                    "İkiHaftadaBir" => 14,
                    _ => 0
                };
            }

            // Create lessons for each recurrence
            for (int recurrence = 0; recurrence < actualTekrarSayisi; recurrence++)
            {
                var recurrenceDate = lesson.Baslangic.AddDays(recurrence * intervalDays);
                var currentStart = recurrenceDate;

                // Ders sayısı kadar ders oluştur (per recurrence)
                for (int i = 0; i < DersSayisi; i++)
                {
                    var currentEnd = currentStart.AddMinutes(40);

                    // Çakışma kontrolü - BLOKLAYICI
                    var conflictingLesson = await _context.Lessons
                        .Include(l => l.Student)
                        .Where(l => (l.Baslangic < currentEnd && l.Bitis > currentStart))
                        .FirstOrDefaultAsync();

                    if (conflictingLesson != null)
                    {
                        hasConflict = true;
                        conflictMessages.Add($"{currentStart:dd/MM/yyyy HH:mm}-{currentEnd:HH:mm} saatinde {conflictingLesson.Student?.AdSoyad} ile çakışma var");
                    }

                    var newLesson = new Lesson
                    {
                        StudentId = lesson.StudentId,
                        Baslangic = currentStart,
                        Bitis = currentEnd,
                        Konu = DersSayisi > 1 ? $"{lesson.Konu} (Ders {i + 1}/{DersSayisi})" : lesson.Konu,
                        Ucret = lesson.Ucret,
                        Durum = lesson.Durum,
                        Not = lesson.Not,
                        OdemeAlindi = lesson.OdemeAlindi,
                        CreatedAt = DateTime.Now,
                        TekrarliMi = TekrarliMi && !string.IsNullOrEmpty(TekrarPeriyodu),
                        TekrarPeriyodu = TekrarliMi ? TekrarPeriyodu : null,
                        TekrarGrupId = tekrarGrupId,
                        TekrarSirasi = recurrence + 1,
                        Tur = lesson.Tur,
                        DersNotu = lesson.DersNotu
                    };

                    lessonsToAdd.Add(newLesson);

                    // Bir sonraki ders için: 40 dk ders + 15 dk mola
                    currentStart = currentEnd.AddMinutes(15);
                }
            }

            // Çakışma varsa kaydetme!
            if (hasConflict)
            {
                TempData["ErrorMessage"] = $"❌ Ders eklenemedi! Çakışma tespit edildi: {string.Join(", ", conflictMessages)}";
                await PopulateStudentsDropDown(lesson.StudentId);
                return View(lesson);
            }

            // Tüm dersleri ekle
            _context.Lessons.AddRange(lessonsToAdd);
            await _context.SaveChangesAsync();

            // Zoom + Google Calendar entegrasyonu
            Console.WriteLine("📅 Zoom ve Google Calendar'a dersler ekleniyor...");

            foreach (var addedLesson in lessonsToAdd)
            {
                var student = await _context.Students.FindAsync(addedLesson.StudentId);
                string title = $"Ders: {addedLesson.Konu}";
                string description = $"Öğrenci: {student?.AdSoyad}";

                Console.WriteLine($"   Öğrenci: {student?.AdSoyad} ({student?.Email})");
                Console.WriteLine($"   Başlık: {title}");
                Console.WriteLine($"   Tarih: {addedLesson.Baslangic} - {addedLesson.Bitis}");

                // Not varsa ekle
                if (!string.IsNullOrEmpty(addedLesson.Not))
                {
                    description += $"\nNot: {addedLesson.Not}";
                }

                // 1. Zoom toplantısı oluştur
                string? zoomJoinUrl = null;
                string? zoomPassword = null;

                var lessonDuration = (int)(addedLesson.Bitis - addedLesson.Baslangic).TotalMinutes;
                var zoomMeeting = await _zoomMeetingService.CreateMeetingAsync(
                    topic: title,
                    startTime: addedLesson.Baslangic,
                    durationMinutes: lessonDuration,
                    agenda: $"{student?.AdSoyad} ile {addedLesson.Konu}"
                );

                if (zoomMeeting != null)
                {
                    Console.WriteLine($"   ✅ Zoom Meeting ID: {zoomMeeting.Id}");
                    Console.WriteLine($"   🔗 Join URL: {zoomMeeting.JoinUrl}");
                    addedLesson.ZoomMeetingId = zoomMeeting.Id;
                    zoomJoinUrl = zoomMeeting.JoinUrl;
                    zoomPassword = zoomMeeting.Password;
                }
                else
                {
                    Console.WriteLine($"   ⚠️ Zoom toplantısı oluşturulamadı, sadece Calendar'a eklenecek");
                }

                // 2. Google Calendar'a ekle (Zoom linki ile)
                var eventId = await _calendarService.AddLessonEventAsync(
                    title,
                    description,
                    addedLesson.Baslangic,
                    addedLesson.Bitis,
                    zoomJoinUrl,
                    zoomPassword
                );

                if (!string.IsNullOrEmpty(eventId))
                {
                    Console.WriteLine($"   ✅ Calendar Event ID: {eventId}");
                    addedLesson.GoogleCalendarEventId = eventId;
                }
                else
                {
                    Console.WriteLine($"   ❌ Calendar'a eklenemedi!");
                }
            }
            await _context.SaveChangesAsync();

            // Log activity
            var firstLesson = lessonsToAdd.First();
            var logStudent = await _context.Students.FindAsync(firstLesson.StudentId);
            await _activityLogService.LogActivityAsync(
                "Create",
                "Lesson",
                null,
                $"{lessonsToAdd.Count} ders eklendi: {logStudent?.AdSoyad} - {firstLesson.Konu}",
                new { Count = lessonsToAdd.Count, Student = logStudent?.AdSoyad, Subject = firstLesson.Konu }
            );

            // Prepare success message
            var zoomCreatedCount = lessonsToAdd.Count(l => l.ZoomMeetingId.HasValue);
            string successMessage;

            if (TekrarliMi && !string.IsNullOrEmpty(TekrarPeriyodu))
            {
                successMessage = $"✅ {lessonsToAdd.Count} adet tekrarlı ders başarıyla oluşturuldu";
            }
            else if (DersSayisi > 1)
            {
                successMessage = $"✅ {DersSayisi} adet ders başarıyla eklendi";
            }
            else
            {
                successMessage = "✅ Ders başarıyla eklendi";
            }

            // Zoom ve Calendar durumunu ekle
            var statusParts = new List<string>();
            if (zoomCreatedCount > 0)
            {
                statusParts.Add($"{zoomCreatedCount} Zoom toplantısı oluşturuldu");
            }
            statusParts.Add("Google Calendar'a senkronize edildi");

            successMessage += ", " + string.Join(" ve ", statusParts) + ".";

            TempData["SuccessMessage"] = successMessage;

            return RedirectToAction(nameof(Index));
        }

        Console.WriteLine("❌ ModelState geçersiz, form tekrar gösteriliyor...");
        await PopulateStudentsDropDown(lesson.StudentId);
        return View(lesson);
    }

    // GET: Lessons/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var lesson = await _context.Lessons.FindAsync(id);
        if (lesson == null)
        {
            return NotFound();
        }

        await PopulateStudentsDropDown(lesson.StudentId);
        return View(lesson);
    }

    // POST: Lessons/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,StudentId,Baslangic,Bitis,Konu,Ucret,Durum,Not,OdemeAlindi,CreatedAt,Tur,DersNotu")] Lesson lesson)
    {
        if (id != lesson.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                // Mevcut dersi yükle (eski saat bilgilerini almak için)
                var existingLesson = await _context.Lessons
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Id == lesson.Id);

                if (existingLesson == null)
                {
                    return NotFound();
                }

                // Çakışma kontrolü - BLOKLAYICI
                var conflictingLesson = await _context.Lessons
                    .Include(l => l.Student)
                    .Where(l => l.Id != lesson.Id)
                    .Where(l => (l.Baslangic < lesson.Bitis && l.Bitis > lesson.Baslangic))
                    .FirstOrDefaultAsync();

                if (conflictingLesson != null)
                {
                    TempData["ErrorMessage"] = $"❌ Ders güncellenemedi! {conflictingLesson.Baslangic:HH:mm}-{conflictingLesson.Bitis:HH:mm} saatinde {conflictingLesson.Student?.AdSoyad} ile çakışma var.";
                    await PopulateStudentsDropDown(lesson.StudentId);
                    return View(lesson);
                }

                var student = await _context.Students.FindAsync(lesson.StudentId);
                string title = $"Ders: {lesson.Konu}";
                string description = $"Öğrenci: {student?.AdSoyad}";

                // Not varsa ekle
                if (!string.IsNullOrEmpty(lesson.Not))
                {
                    description += $"\nNot: {lesson.Not}";
                }

                // Zoom meeting var mı kontrol et ve saat değiştiyse güncelle
                string? zoomJoinUrl = null;
                string? zoomPassword = null;
                bool zoomUpdated = false;

                if (existingLesson.ZoomMeetingId.HasValue)
                {
                    // Saat değişti mi kontrol et
                    bool timeChanged = existingLesson.Baslangic != lesson.Baslangic ||
                                      existingLesson.Bitis != lesson.Bitis;

                    if (timeChanged)
                    {
                        // Zoom meeting'i güncelle
                        var lessonDuration = (int)(lesson.Bitis - lesson.Baslangic).TotalMinutes;
                        zoomUpdated = await _zoomMeetingService.UpdateMeetingAsync(
                            existingLesson.ZoomMeetingId.Value,
                            lesson.Baslangic,
                            lessonDuration,
                            "Europe/Istanbul"
                        );
                    }

                    // Güncel Zoom bilgilerini al
                    var zoomMeeting = await _zoomMeetingService.GetMeetingAsync(existingLesson.ZoomMeetingId.Value);
                    if (zoomMeeting != null)
                    {
                        zoomJoinUrl = zoomMeeting.JoinUrl;
                        zoomPassword = zoomMeeting.Password;
                    }

                    // ZoomMeetingId'yi koru
                    lesson.ZoomMeetingId = existingLesson.ZoomMeetingId;
                }

                // Google Calendar Event ID'yi koru
                lesson.GoogleCalendarEventId = existingLesson.GoogleCalendarEventId;

                // Google Calendar'ı güncelle
                if (!string.IsNullOrEmpty(lesson.GoogleCalendarEventId))
                {
                    await _calendarService.UpdateLessonEventAsync(
                        lesson.GoogleCalendarEventId,
                        title,
                        description,
                        lesson.Baslangic,
                        lesson.Bitis,
                        zoomJoinUrl,
                        zoomPassword
                    );
                }

                lesson.UpdatedAt = DateTime.Now;
                _context.Update(lesson);
                await _context.SaveChangesAsync();

                // Log activity
                var editStudent = await _context.Students.FindAsync(lesson.StudentId);
                await _activityLogService.LogActivityAsync(
                    "Update",
                    "Lesson",
                    lesson.Id,
                    $"Ders güncellendi: {editStudent?.AdSoyad} - {lesson.Konu}",
                    new { lesson.Konu, lesson.Durum, Start = lesson.Baslangic, End = lesson.Bitis }
                );

                TempData["SuccessMessage"] = "Ders bilgileri güncellendi ve Google Calendar senkronize edildi.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LessonExists(lesson.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }

        await PopulateStudentsDropDown(lesson.StudentId);
        return View(lesson);
    }

    // GET: Lessons/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var lesson = await _context.Lessons
            .Include(l => l.Student)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (lesson == null)
        {
            return NotFound();
        }

        return View(lesson);
    }

    // POST: Lessons/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var lesson = await _context.Lessons.FindAsync(id);
        if (lesson != null)
        {
            Console.WriteLine($"=== DERS SİLME İŞLEMİ ===");
            Console.WriteLine($"Lesson ID: {lesson.Id}");
            Console.WriteLine($"Google Calendar Event ID: {lesson.GoogleCalendarEventId ?? "YOK"}");
            Console.WriteLine($"Zoom Meeting ID: {lesson.ZoomMeetingId?.ToString() ?? "YOK"}");

            var deletedFromCalendar = false;
            var deletedFromZoom = false;

            // Google Calendar'dan sil
            if (!string.IsNullOrEmpty(lesson.GoogleCalendarEventId))
            {
                Console.WriteLine($"Google Calendar'dan siliniyor: {lesson.GoogleCalendarEventId}");
                deletedFromCalendar = await _calendarService.DeleteLessonEventAsync(lesson.GoogleCalendarEventId);
                Console.WriteLine($"Calendar silme sonucu: {deletedFromCalendar}");
            }

            // Zoom'dan sil
            if (lesson.ZoomMeetingId.HasValue)
            {
                Console.WriteLine($"Zoom'dan siliniyor: {lesson.ZoomMeetingId.Value}");
                deletedFromZoom = await _zoomMeetingService.DeleteMeetingAsync(lesson.ZoomMeetingId.Value);
                Console.WriteLine($"Zoom silme sonucu: {deletedFromZoom}");
            }
            else
            {
                Console.WriteLine("Zoom Meeting ID yok, Zoom'dan silinmeyecek");
            }

            var deleteStudent = await _context.Students.FindAsync(lesson.StudentId);
            var deleteLessonInfo = new { lesson.Konu, Student = deleteStudent?.AdSoyad, Start = lesson.Baslangic };

            _context.Lessons.Remove(lesson);
            await _context.SaveChangesAsync();

            // Log activity
            await _activityLogService.LogActivityAsync(
                "Delete",
                "Lesson",
                lesson.Id,
                $"Ders silindi: {deleteLessonInfo.Student} - {deleteLessonInfo.Konu}",
                deleteLessonInfo
            );

            // Başarı mesajı
            var successParts = new List<string> { "Ders silindi" };
            if (deletedFromCalendar) successParts.Add("Google Calendar'dan kaldırıldı");
            if (deletedFromZoom) successParts.Add("Zoom toplantısı iptal edildi");

            // Debug bilgisi ekle
            var debugInfo = $" (Zoom ID: {(lesson.ZoomMeetingId.HasValue ? lesson.ZoomMeetingId.Value.ToString() : "YOK")}, ";
            debugInfo += $"Calendar: {(deletedFromCalendar ? "✓" : "✗")}, ";
            debugInfo += $"Zoom: {(deletedFromZoom ? "✓" : "✗")})";

            TempData["SuccessMessage"] = string.Join(" ve ", successParts) + "." + debugInfo;
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Lessons/Complete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var lesson = await _context.Lessons.Include(l => l.Student).FirstOrDefaultAsync(l => l.Id == id);
        if (lesson != null)
        {
            lesson.Durum = "Tamamlandı";
            lesson.UpdatedAt = DateTime.Now;
            _context.Update(lesson);

            // Eğer ödeme alınmamışsa, otomatik olarak ödeme kaydı oluştur
            if (!lesson.OdemeAlindi && lesson.Ucret > 0)
            {
                var payment = new Payment
                {
                    StudentId = lesson.StudentId,
                    Tarih = lesson.Baslangic.Date,
                    Tutar = lesson.Ucret,
                    Donem = "Haftalık",
                    Aciklama = $"Tamamlanan ders: {lesson.Konu} ({lesson.Baslangic:dd/MM/yyyy HH:mm})",
                    Durum = PaymentStatus.Beklemede,
                    CreatedAt = DateTime.Now
                };

                _context.Payments.Add(payment);

                // Activity log
                await _activityLogService.LogActivityAsync(
                    "Create",
                    "Payment",
                    null,
                    $"Tamamlanan ders için ödeme kaydı oluşturuldu: {lesson.Student?.AdSoyad} - {lesson.Ucret:C2}",
                    new { lesson.Konu, Amount = lesson.Ucret, Student = lesson.Student?.AdSoyad }
                );
            }

            await _context.SaveChangesAsync();

            var successMessage = lesson.OdemeAlindi || lesson.Ucret == 0
                ? "Ders tamamlandı olarak işaretlendi."
                : "Ders tamamlandı ve ödeme kaydı oluşturuldu.";

            TempData["SuccessMessage"] = successMessage;
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Lessons/Cancel/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var lesson = await _context.Lessons.Include(l => l.Student).FirstOrDefaultAsync(l => l.Id == id);
        if (lesson != null)
        {
            Console.WriteLine($"=== DERS İPTAL İŞLEMİ ===");
            Console.WriteLine($"Lesson ID: {lesson.Id}");
            Console.WriteLine($"Google Calendar Event ID: {lesson.GoogleCalendarEventId ?? "YOK"}");
            Console.WriteLine($"Zoom Meeting ID: {lesson.ZoomMeetingId?.ToString() ?? "YOK"}");

            var deletedFromCalendar = false;
            var deletedFromZoom = false;

            // Google Calendar'dan sil
            if (!string.IsNullOrEmpty(lesson.GoogleCalendarEventId))
            {
                Console.WriteLine($"Google Calendar'dan siliniyor: {lesson.GoogleCalendarEventId}");
                deletedFromCalendar = await _calendarService.DeleteLessonEventAsync(lesson.GoogleCalendarEventId);
                Console.WriteLine($"Calendar silme sonucu: {deletedFromCalendar}");
                lesson.GoogleCalendarEventId = null; // Event ID'yi temizle
            }

            // Zoom'dan sil
            if (lesson.ZoomMeetingId.HasValue)
            {
                Console.WriteLine($"Zoom'dan siliniyor: {lesson.ZoomMeetingId.Value}");
                deletedFromZoom = await _zoomMeetingService.DeleteMeetingAsync(lesson.ZoomMeetingId.Value);
                Console.WriteLine($"Zoom silme sonucu: {deletedFromZoom}");
                lesson.ZoomMeetingId = null; // Meeting ID'yi temizle
            }
            else
            {
                Console.WriteLine("Zoom Meeting ID yok, Zoom'dan silinmeyecek");
            }

            lesson.Durum = "İptal";
            lesson.UpdatedAt = DateTime.Now;
            _context.Update(lesson);
            await _context.SaveChangesAsync();

            // Başarı mesajı
            var successParts = new List<string> { "Ders iptal edildi" };
            if (deletedFromCalendar) successParts.Add("Google Calendar'dan kaldırıldı");
            if (deletedFromZoom) successParts.Add("Zoom toplantısı iptal edildi");

            TempData["SuccessMessage"] = string.Join(" ve ", successParts) + ".";
        }

        return RedirectToAction(nameof(Index));
    }

    private bool LessonExists(int id)
    {
        return _context.Lessons.Any(e => e.Id == id);
    }

    // API: Çakışma kontrolü
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CheckConflict([FromBody] ConflictCheckRequest request)
    {
        var dersSayisi = request.DersSayisi;
        var currentStart = request.Baslangic;
        var conflicts = new List<ConflictInfo>();

        for (int i = 0; i < dersSayisi; i++)
        {
            var currentEnd = currentStart.AddMinutes(40);

            var conflictingLesson = await _context.Lessons
                .Include(l => l.Student)
                .Where(l => l.Baslangic < currentEnd && l.Bitis > currentStart)
                .FirstOrDefaultAsync();

            if (conflictingLesson != null)
            {
                conflicts.Add(new ConflictInfo
                {
                    Start = currentStart.ToString("HH:mm"),
                    End = currentEnd.ToString("HH:mm"),
                    ConflictingStart = conflictingLesson.Baslangic.ToString("HH:mm"),
                    ConflictingEnd = conflictingLesson.Bitis.ToString("HH:mm"),
                    StudentName = conflictingLesson.Student?.AdSoyad ?? "Bilinmeyen",
                    Subject = conflictingLesson.Konu
                });
            }

            currentStart = currentEnd.AddMinutes(15);
        }

        return Json(new { hasConflict = conflicts.Any(), conflicts });
    }

    // API: Ders zamanını güncelle (Drag & Drop için)
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UpdateLessonTime([FromBody] UpdateLessonTimeRequest request)
    {
        try
        {
            Console.WriteLine("=== DRAG & DROP GÜNCELLEME ===");
            Console.WriteLine($"Lesson ID: {request.Id}");
            Console.WriteLine($"Yeni Başlangıç: {request.NewStart}");
            Console.WriteLine($"Yeni Bitiş: {request.NewEnd}");

            var lesson = await _context.Lessons
                .Include(l => l.Student)
                .FirstOrDefaultAsync(l => l.Id == request.Id);

            if (lesson == null)
            {
                Console.WriteLine("❌ Ders bulunamadı!");
                return NotFound(new { success = false, message = "Ders bulunamadı" });
            }

            Console.WriteLine($"Ders bulundu: {lesson.Konu}, Öğrenci: {lesson.Student?.AdSoyad}");
            Console.WriteLine($"Eski zaman: {lesson.Baslangic} - {lesson.Bitis}");
            Console.WriteLine($"Google Calendar Event ID: {lesson.GoogleCalendarEventId ?? "YOK"}");

            // Yeni başlangıç ve bitiş zamanları - UTC'den Türkiye saatine çevir
            var turkeyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            var newStart = TimeZoneInfo.ConvertTimeFromUtc(request.NewStart.ToUniversalTime(), turkeyTimeZone);
            var newEnd = TimeZoneInfo.ConvertTimeFromUtc(request.NewEnd.ToUniversalTime(), turkeyTimeZone);

            Console.WriteLine($"Gelen UTC zaman: {request.NewStart:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Türkiye saatine çevrildi: {newStart:yyyy-MM-dd HH:mm:ss}");

            // Çakışma kontrolü - BLOKLAYICI
            var conflictingLesson = await _context.Lessons
                .Include(l => l.Student)
                .Where(l => l.Id != lesson.Id) // Kendi dersi hariç
                .Where(l => (l.Baslangic < newEnd && l.Bitis > newStart))
                .FirstOrDefaultAsync();

            if (conflictingLesson != null)
            {
                Console.WriteLine($"❌ Çakışma tespit edildi: {conflictingLesson.Student?.AdSoyad}");
                return Ok(new
                {
                    success = false,
                    message = $"❌ Bu saatte {conflictingLesson.Student?.AdSoyad} ile çakışma var ({conflictingLesson.Baslangic:HH:mm}-{conflictingLesson.Bitis:HH:mm})"
                });
            }

            // Dersi güncelle
            lesson.Baslangic = newStart;
            lesson.Bitis = newEnd;
            lesson.UpdatedAt = DateTime.Now;

            Console.WriteLine($"Yeni zaman: {lesson.Baslangic} - {lesson.Bitis}");

            // Zoom meeting var mı kontrol et ve güncelle
            string? zoomJoinUrl = null;
            string? zoomPassword = null;
            bool zoomUpdated = false;

            if (lesson.ZoomMeetingId.HasValue)
            {
                Console.WriteLine($"🔄 Zoom meeting güncelleme başlatılıyor: ID={lesson.ZoomMeetingId.Value}");

                // Dersin yeni süresini hesapla
                var lessonDuration = (int)(lesson.Bitis - lesson.Baslangic).TotalMinutes;

                // Zoom toplantısını güncelle
                zoomUpdated = await _zoomMeetingService.UpdateMeetingAsync(
                    lesson.ZoomMeetingId.Value,
                    lesson.Baslangic,
                    lessonDuration,
                    "Europe/Istanbul"
                );

                if (zoomUpdated)
                {
                    Console.WriteLine($"✅ Zoom meeting başarıyla güncellendi!");

                    // Güncellenmiş meeting bilgilerini al
                    var zoomMeeting = await _zoomMeetingService.GetMeetingAsync(lesson.ZoomMeetingId.Value);
                    if (zoomMeeting != null)
                    {
                        zoomJoinUrl = zoomMeeting.JoinUrl;
                        zoomPassword = zoomMeeting.Password;
                        Console.WriteLine($"   Zoom Link: {zoomJoinUrl}");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ Zoom meeting güncellenemedi!");
                }
            }

            // Google Calendar'ı güncelle
            bool calendarUpdated = false;
            if (!string.IsNullOrEmpty(lesson.GoogleCalendarEventId))
            {
                Console.WriteLine("📅 Google Calendar güncelleme başlatılıyor...");
                string title = $"Ders: {lesson.Konu}";
                string description = $"Öğrenci: {lesson.Student?.AdSoyad}";

                if (!string.IsNullOrEmpty(lesson.Not))
                {
                    description += $"\nNot: {lesson.Not}";
                }

                Console.WriteLine($"   Event ID: {lesson.GoogleCalendarEventId}");
                Console.WriteLine($"   Title: {title}");
                Console.WriteLine($"   Start: {lesson.Baslangic}");
                Console.WriteLine($"   End: {lesson.Bitis}");

                calendarUpdated = await _calendarService.UpdateLessonEventAsync(
                    lesson.GoogleCalendarEventId,
                    title,
                    description,
                    lesson.Baslangic,
                    lesson.Bitis,
                    zoomJoinUrl,
                    zoomPassword
                );

                if (calendarUpdated)
                {
                    Console.WriteLine("✅ Google Calendar başarıyla güncellendi!");
                }
                else
                {
                    Console.WriteLine("⚠️ Google Calendar güncellenemedi!");
                }
            }
            else
            {
                Console.WriteLine("ℹ️ Google Calendar Event ID yok, calendar güncellenmedi");
            }

            _context.Update(lesson);
            await _context.SaveChangesAsync();

            Console.WriteLine("✅ Veritabanı güncellendi!");

            // Kullanıcıya bilgilendirme mesajı hazırla
            string message = "Ders zamanı güncellendi";

            if (lesson.ZoomMeetingId.HasValue || !string.IsNullOrEmpty(lesson.GoogleCalendarEventId))
            {
                var syncDetails = new List<string>();

                if (lesson.ZoomMeetingId.HasValue)
                {
                    syncDetails.Add(zoomUpdated ? "Zoom ✅" : "Zoom ⚠️");
                }

                if (!string.IsNullOrEmpty(lesson.GoogleCalendarEventId))
                {
                    syncDetails.Add(calendarUpdated ? "Calendar ✅" : "Calendar ⚠️");
                }

                message += $" ({string.Join(", ", syncDetails)})";
            }

            return Ok(new
            {
                success = true,
                message = message,
                zoomUpdated = lesson.ZoomMeetingId.HasValue ? zoomUpdated : (bool?)null,
                calendarUpdated = !string.IsNullOrEmpty(lesson.GoogleCalendarEventId) ? calendarUpdated : (bool?)null
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ HATA: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            return StatusCode(500, new
            {
                success = false,
                message = $"Bir hata oluştu: {ex.Message}"
            });
        }
    }

    private async Task PopulateStudentsDropDown(object? selectedStudent = null)
    {
        var userId = await GetCurrentUserIdAsync();

        var students = await _context.Students
            .Where(s => s.AktifMi && s.UserId == userId) // Sadece kullanıcının kendi öğrencileri
            .OrderBy(s => s.Ad)
            .Select(s => new
            {
                s.Id,
                FullName = s.Ad + " " + s.Soyad
            })
            .ToListAsync();

        ViewBag.StudentId = new SelectList(students, "Id", "FullName", selectedStudent);
    }
}

public class ConflictCheckRequest
{
    public DateTime Baslangic { get; set; }
    public int DersSayisi { get; set; }
}

public class ConflictInfo
{
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public string ConflictingStart { get; set; } = string.Empty;
    public string ConflictingEnd { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
}

public class UpdateLessonTimeRequest
{
    public int Id { get; set; }
    public DateTime NewStart { get; set; }
    public DateTime NewEnd { get; set; }
}
