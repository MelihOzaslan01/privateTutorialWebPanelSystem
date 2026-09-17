using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateTutorTracker.Data;
using PrivateTutorTracker.Models;
using PrivateTutorTracker.Models.ViewModels;
using PrivateTutorTracker.Services;

namespace PrivateTutorTracker.Controllers;

[Authorize]
public class StudentsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IActivityLogService _activityLogService;
    private readonly UserManager<ApplicationUser> _userManager;

    public StudentsController(
        AppDbContext context,
        IEmailService emailService,
        IActivityLogService activityLogService,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _emailService = emailService;
        _activityLogService = activityLogService;
        _userManager = userManager;
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id ?? string.Empty;
    }

    // GET: Students
    public async Task<IActionResult> Index(bool showInactive = false)
    {
        var userId = await GetCurrentUserIdAsync();

        // Sadece mevcut kullanıcının öğrencilerini çek
        var query = _context.Students.Where(s => s.UserId == userId);

        // Filtreleme: showInactive false ise sadece aktif öğrencileri göster
        if (!showInactive)
        {
            query = query.Where(s => s.AktifMi);
        }

        var students = await query
            .OrderBy(s => s.Ad)
            .ToListAsync();

        ViewBag.ShowInactive = showInactive;
        return View(students);
    }

    // GET: Students/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var userId = await GetCurrentUserIdAsync();

        var student = await _context.Students
            .Include(s => s.Lessons.OrderByDescending(l => l.Baslangic))
            .Include(s => s.Payments.OrderByDescending(p => p.Tarih))
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

        if (student == null)
        {
            return NotFound();
        }

        // İstatistik hesaplamaları
        var totalLessons = student.Lessons.Count(l => l.Durum == "Tamamlandı");
        var totalIncome = student.Lessons.Where(l => l.Durum == "Tamamlandı").Sum(l => l.Ucret);
        var lastLessonDate = student.Lessons
            .Where(l => l.Durum == "Tamamlandı")
            .OrderByDescending(l => l.Bitis)
            .FirstOrDefault()?.Bitis;

        ViewBag.TotalLessons = totalLessons;
        ViewBag.TotalIncome = totalIncome;
        ViewBag.LastLessonDate = lastLessonDate;

        return View(student);
    }

    // GET: Students/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Students/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Ad,Soyad,Email,Telefon,Not,AktifMi")] Student student)
    {
        // UserId manuel olarak atanacağı için ModelState'den kaldır
        ModelState.Remove("UserId");
        ModelState.Remove("User");

        if (ModelState.IsValid)
        {
            student.CreatedAt = DateTime.Now;
            student.UserId = await GetCurrentUserIdAsync(); // Mevcut kullanıcıya ata
            // Yeni öğrenciler varsayılan olarak aktif olsun
            student.AktifMi = true;
            _context.Add(student);
            await _context.SaveChangesAsync();

            // Log activity
            await _activityLogService.LogActivityAsync(
                "Create",
                "Student",
                student.Id,
                $"Yeni öğrenci eklendi: {student.AdSoyad}",
                new { student.Ad, student.Soyad, student.Email }
            );

            TempData["SuccessMessage"] = "Öğrenci başarıyla eklendi.";
            return RedirectToAction(nameof(Index));
        }
        return View(student);
    }

    // GET: Students/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var student = await _context.Students.FindAsync(id);
        if (student == null)
        {
            return NotFound();
        }
        return View(student);
    }

    // POST: Students/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Ad,Soyad,Email,Telefon,Not,UcretCustom,AktifMi,CreatedAt")] Student student)
    {
        if (id != student.Id)
        {
            return NotFound();
        }

        // UserId manuel olarak atanacağı için ModelState'den kaldır
        ModelState.Remove("UserId");
        ModelState.Remove("User");

        if (ModelState.IsValid)
        {
            try
            {
                // Mevcut öğrenciyi yükle ve sadece izin verilen alanları güncelle
                var existingStudent = await _context.Students.FindAsync(id);
                if (existingStudent == null)
                {
                    return NotFound();
                }

                // UserId'yi koru - değiştirilemez!
                existingStudent.Ad = student.Ad;
                existingStudent.Soyad = student.Soyad;
                existingStudent.Email = student.Email;
                existingStudent.Telefon = student.Telefon;
                existingStudent.Not = student.Not;
                existingStudent.AktifMi = student.AktifMi;
                existingStudent.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // Log activity
                await _activityLogService.LogActivityAsync(
                    "Update",
                    "Student",
                    existingStudent.Id,
                    $"Öğrenci güncellendi: {existingStudent.AdSoyad}",
                    new { existingStudent.Ad, existingStudent.Soyad, existingStudent.Email, existingStudent.AktifMi }
                );

                TempData["SuccessMessage"] = "Öğrenci bilgileri güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StudentExists(student.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }
        return View(student);
    }

    // GET: Students/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(m => m.Id == id);
        if (student == null)
        {
            return NotFound();
        }

        return View(student);
    }

    // POST: Students/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var student = await _context.Students.FindAsync(id);
        if (student != null)
        {
            // Soft delete - sadece aktif değilini false yapalım
            student.AktifMi = false;
            student.UpdatedAt = DateTime.Now;
            _context.Update(student);
            await _context.SaveChangesAsync();

            // Log activity
            await _activityLogService.LogActivityAsync(
                "Delete",
                "Student",
                student.Id,
                $"Öğrenci pasif hale getirildi: {student.AdSoyad}",
                new { student.Ad, student.Soyad }
            );

            TempData["SuccessMessage"] = "Öğrenci pasif hale getirildi.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Students/Activate/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        var student = await _context.Students.FindAsync(id);
        if (student != null)
        {
            student.AktifMi = true;
            student.UpdatedAt = DateTime.Now;
            _context.Update(student);
            await _context.SaveChangesAsync();

            // Log activity
            await _activityLogService.LogActivityAsync(
                "Activate",
                "Student",
                student.Id,
                $"Öğrenci aktif hale getirildi: {student.AdSoyad}",
                new { student.Ad, student.Soyad }
            );

            TempData["SuccessMessage"] = "Öğrenci aktif hale getirildi.";
        }

        return RedirectToAction(nameof(Index), new { showInactive = true });
    }

    // GET: Students/PermanentDelete/5
    public async Task<IActionResult> PermanentDelete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var student = await _context.Students
            .Include(s => s.Lessons)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (student == null)
        {
            return NotFound();
        }

        return View(student);
    }

    // POST: Students/PermanentDelete/5
    [HttpPost, ActionName("PermanentDelete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PermanentDeleteConfirmed(int id)
    {
        var student = await _context.Students
            .Include(s => s.Lessons)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student != null)
        {
            var studentName = student.AdSoyad;
            var lessonCount = student.Lessons.Count;
            var paymentCount = student.Payments.Count;

            // Önce ilişkili kayıtları sil
            _context.Lessons.RemoveRange(student.Lessons);
            _context.Payments.RemoveRange(student.Payments);

            // Sonra öğrenciyi sil
            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            // Log activity
            await _activityLogService.LogActivityAsync(
                "PermanentDelete",
                "Student",
                id,
                $"Öğrenci kalıcı olarak silindi: {studentName}",
                new { studentName, lessonCount, paymentCount }
            );

            TempData["SuccessMessage"] = $"Öğrenci kalıcı olarak silindi: {studentName} ({lessonCount} ders, {paymentCount} ödeme kaydı ile birlikte)";
        }

        return RedirectToAction(nameof(Index), new { showInactive = true });
    }

    private bool StudentExists(int id)
    {
        return _context.Students.Any(e => e.Id == id);
    }

    // GET: Students/SendEmail/5
    public async Task<IActionResult> SendEmail(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var student = await _context.Students.FindAsync(id);
        if (student == null)
        {
            return NotFound();
        }

        var model = new SendEmailViewModel
        {
            StudentId = student.Id,
            StudentName = $"{student.Ad} {student.Soyad}",
            ToEmail = student.Email ?? ""
        };

        return View(model);
    }

    // POST: Students/SendEmail
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmail(SendEmailViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var student = await _context.Students.FindAsync(model.StudentId);
        if (student == null)
        {
            return NotFound();
        }

        // HTML email body oluştur
        var emailBody = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2 style='color: #0d6efd;'>Özel Ders Takip Sistemi</h2>
                <p>Merhaba <strong>{student.Ad} {student.Soyad}</strong>,</p>
                <div style='margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #0d6efd;'>
                    {model.Body.Replace("\n", "<br>")}
                </div>
                <hr>
                <p style='color: #6c757d; font-size: 12px;'>
                    Bu mail Özel Ders Takip Sistemi tarafından gönderilmiştir.
                </p>
            </body>
            </html>
        ";

        var result = await _emailService.SendEmailAsync(model.ToEmail, student.Ad + " " + student.Soyad, model.Subject, emailBody);

        if (result)
        {
            TempData["SuccessMessage"] = $"Email başarıyla gönderildi: {model.ToEmail}";
            return RedirectToAction(nameof(Details), new { id = model.StudentId });
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Email gönderilirken bir hata oluştu. Lütfen email ayarlarınızı kontrol edin.");
            return View(model);
        }
    }

    // GET: Students/SendBulkEmail
    public async Task<IActionResult> SendBulkEmail()
    {
        var activeStudents = await _context.Students
            .Where(s => s.AktifMi && !string.IsNullOrEmpty(s.Email))
            .OrderBy(s => s.Ad)
            .ToListAsync();

        var model = new SendBulkEmailViewModel
        {
            TotalStudents = activeStudents.Count
        };

        return View(model);
    }

    // POST: Students/SendBulkEmail
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendBulkEmail(SendBulkEmailViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var activeStudents = await _context.Students
            .Where(s => s.AktifMi && !string.IsNullOrEmpty(s.Email))
            .ToListAsync();

        if (!activeStudents.Any())
        {
            ModelState.AddModelError(string.Empty, "Email gönderilecek aktif öğrenci bulunamadı.");
            return View(model);
        }

        var emailAddresses = activeStudents.Select(s => s.Email!).ToList();

        // HTML email body oluştur
        var emailBody = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2 style='color: #0d6efd;'>Özel Ders Takip Sistemi</h2>
                <p>Merhaba,</p>
                <div style='margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #0d6efd;'>
                    {model.Body.Replace("\n", "<br>")}
                </div>
                <hr>
                <p style='color: #6c757d; font-size: 12px;'>
                    Bu mail Özel Ders Takip Sistemi tarafından gönderilmiştir.
                </p>
            </body>
            </html>
        ";

        var result = await _emailService.SendBulkEmailAsync(emailAddresses, model.Subject, emailBody);

        if (result)
        {
            TempData["SuccessMessage"] = $"Toplu email başarıyla gönderildi. Toplam: {activeStudents.Count} öğrenci";
            return RedirectToAction(nameof(Index));
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Bazı emailler gönderilemedi. Detaylar için log'ları kontrol edin.");
            model.TotalStudents = activeStudents.Count;
            return View(model);
        }
    }
}
