using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PrivateTutorTracker.Data;
using PrivateTutorTracker.Models;
using PrivateTutorTracker.Services;
using System.Security.Claims;

namespace PrivateTutorTracker.Controllers;

[Authorize]
public class PaymentsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IActivityLogService _activityLogService;

    public PaymentsController(AppDbContext context, IActivityLogService activityLogService)
    {
        _context = context;
        _activityLogService = activityLogService;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // GET: Payments
    public async Task<IActionResult> Index(
        int[]? studentIds = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string sortBy = "Tarih",
        string sortOrder = "desc")
    {
        var query = _context.Payments
            .Include(p => p.Student)
            .Where(p => p.Student!.UserId == CurrentUserId)
            .AsQueryable();

        // Çoklu öğrenci filtresi
        if (studentIds != null && studentIds.Length > 0)
        {
            query = query.Where(p => studentIds.Contains(p.StudentId));
        }

        if (startDate.HasValue)
        {
            query = query.Where(p => p.Tarih >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(p => p.Tarih <= endDate.Value);
        }

        // Sıralama
        query = sortBy switch
        {
            "Student" => sortOrder == "asc"
                ? query.OrderBy(p => p.Student!.Ad).ThenBy(p => p.Student!.Soyad)
                : query.OrderByDescending(p => p.Student!.Ad).ThenByDescending(p => p.Student!.Soyad),
            "Tutar" => sortOrder == "asc"
                ? query.OrderBy(p => p.Tutar)
                : query.OrderByDescending(p => p.Tutar),
            "Durum" => sortOrder == "asc"
                ? query.OrderBy(p => p.Durum)
                : query.OrderByDescending(p => p.Durum),
            "Tarih" => sortOrder == "asc"
                ? query.OrderBy(p => p.Tarih)
                : query.OrderByDescending(p => p.Tarih),
            _ => query.OrderByDescending(p => p.Tarih)
        };

        var payments = await query.ToListAsync();

        ViewBag.Students = await _context.Students
            .Where(s => s.AktifMi && s.UserId == CurrentUserId)
            .OrderBy(s => s.Ad)
            .ToListAsync();

        ViewBag.SelectedStudentIds = studentIds ?? Array.Empty<int>();
        ViewBag.CurrentStartDate = startDate;
        ViewBag.CurrentEndDate = endDate;
        ViewBag.CurrentSortBy = sortBy;
        ViewBag.CurrentSortOrder = sortOrder;
        ViewBag.TotalAmount = payments.Sum(p => p.Tutar);

        return View(payments);
    }

    // GET: Payments/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var payment = await _context.Payments
            .Include(p => p.Student)
            .FirstOrDefaultAsync(m => m.Id == id && m.Student!.UserId == CurrentUserId);

        if (payment == null)
        {
            return NotFound();
        }

        return View(payment);
    }

    // GET: Payments/Create
    public async Task<IActionResult> Create(int? studentId)
    {
        await PopulateStudentsDropDown(studentId);

        var payment = new Payment
        {
            Tarih = DateTime.Now,
            StudentId = studentId ?? 0
        };

        return View(payment);
    }

    // POST: Payments/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("StudentId,Tarih,Tutar,Donem,Aciklama,Durum")] Payment payment)
    {
        if (!await _context.Students.AnyAsync(s => s.Id == payment.StudentId && s.UserId == CurrentUserId))
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            payment.CreatedAt = DateTime.Now;
            _context.Add(payment);
            await _context.SaveChangesAsync();

            // Log activity
            var student = await _context.Students.FindAsync(payment.StudentId);
            await _activityLogService.LogActivityAsync(
                "Create",
                "Payment",
                payment.Id,
                $"Yeni ödeme kaydı eklendi: {student?.AdSoyad} - {payment.Tutar:C}",
                new { payment.StudentId, StudentName = student?.AdSoyad, payment.Tutar, payment.Tarih, payment.Donem }
            );

            TempData["SuccessMessage"] = "Ödeme kaydı başarıyla eklendi.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateStudentsDropDown(payment.StudentId);
        return View(payment);
    }

    // GET: Payments/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var payment = await _context.Payments
            .Include(p => p.Student)
            .FirstOrDefaultAsync(p => p.Id == id && p.Student!.UserId == CurrentUserId);
        if (payment == null)
        {
            return NotFound();
        }

        await PopulateStudentsDropDown(payment.StudentId);
        return View(payment);
    }

    // POST: Payments/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,StudentId,Tarih,Tutar,Donem,Aciklama,CreatedAt,Durum")] Payment payment)
    {
        if (id != payment.Id)
        {
            return NotFound();
        }

        if (!await _context.Students.AnyAsync(s => s.Id == payment.StudentId && s.UserId == CurrentUserId) ||
            !await _context.Payments.AnyAsync(p => p.Id == id && p.Student!.UserId == CurrentUserId))
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                payment.UpdatedAt = DateTime.Now;
                _context.Update(payment);
                await _context.SaveChangesAsync();

                // Log activity
                var student = await _context.Students.FindAsync(payment.StudentId);
                await _activityLogService.LogActivityAsync(
                    "Update",
                    "Payment",
                    payment.Id,
                    $"Ödeme kaydı güncellendi: {student?.AdSoyad} - {payment.Tutar:C}",
                    new { payment.StudentId, StudentName = student?.AdSoyad, payment.Tutar, payment.Tarih, payment.Donem, payment.Durum }
                );

                TempData["SuccessMessage"] = "Ödeme kaydı güncellendi.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PaymentExists(payment.Id))
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

        await PopulateStudentsDropDown(payment.StudentId);
        return View(payment);
    }

    // GET: Payments/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var payment = await _context.Payments
            .Include(p => p.Student)
            .FirstOrDefaultAsync(m => m.Id == id && m.Student!.UserId == CurrentUserId);

        if (payment == null)
        {
            return NotFound();
        }

        return View(payment);
    }

    // POST: Payments/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var payment = await _context.Payments
            .Include(p => p.Student)
            .FirstOrDefaultAsync(p => p.Id == id && p.Student!.UserId == CurrentUserId);
        if (payment != null)
        {
            // Get student info before deleting
            var student = await _context.Students.FindAsync(payment.StudentId);
            var paymentInfo = new { payment.StudentId, StudentName = student?.AdSoyad, payment.Tutar, payment.Tarih };

            _context.Payments.Remove(payment);
            await _context.SaveChangesAsync();

            // Log activity
            await _activityLogService.LogActivityAsync(
                "Delete",
                "Payment",
                payment.Id,
                $"Ödeme kaydı silindi: {paymentInfo.StudentName} - {paymentInfo.Tutar:C}",
                paymentInfo
            );

            TempData["SuccessMessage"] = "Ödeme kaydı silindi.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Payments/MarkAsPaid
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsPaid([FromForm] string selectedIds)
    {
        if (string.IsNullOrEmpty(selectedIds))
        {
            TempData["ErrorMessage"] = "Lütfen en az bir ödeme seçiniz.";
            return RedirectToAction(nameof(Index));
        }

        var ids = selectedIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (!ids.Any())
        {
            TempData["ErrorMessage"] = "Geçersiz ödeme seçimi.";
            return RedirectToAction(nameof(Index));
        }

        var payments = await _context.Payments
            .Include(p => p.Student)
            .Where(p => ids.Contains(p.Id) && p.Student!.UserId == CurrentUserId)
            .ToListAsync();

        if (!payments.Any())
        {
            TempData["ErrorMessage"] = "Seçilen ödemeler bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        int updatedCount = 0;
        foreach (var payment in payments)
        {
            if (payment.Durum != PaymentStatus.Tamamlandi)
            {
                payment.Durum = PaymentStatus.Tamamlandi;
                payment.UpdatedAt = DateTime.Now;
                _context.Update(payment);
                updatedCount++;
            }
        }

        await _context.SaveChangesAsync();

        // Log activity
        await _activityLogService.LogActivityAsync(
            "BulkUpdate",
            "Payment",
            null,
            $"Toplu ödeme güncellendi: {updatedCount} ödeme tamamlandı olarak işaretlendi",
            new { UpdatedCount = updatedCount, PaymentIds = ids }
        );

        TempData["SuccessMessage"] = $"{updatedCount} ödeme başarıyla tamamlandı olarak işaretlendi.";
        return RedirectToAction(nameof(Index));
    }

    private bool PaymentExists(int id)
    {
        return _context.Payments.Any(e => e.Id == id);
    }

    private async Task PopulateStudentsDropDown(object? selectedStudent = null)
    {
        var students = await _context.Students
            .Where(s => s.AktifMi && s.UserId == CurrentUserId)
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
