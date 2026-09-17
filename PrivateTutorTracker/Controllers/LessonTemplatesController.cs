using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateTutorTracker.Data;
using PrivateTutorTracker.Models;
using PrivateTutorTracker.Services;

namespace PrivateTutorTracker.Controllers;

[Authorize]
public class LessonTemplatesController : Controller
{
    private readonly AppDbContext _context;
    private readonly IActivityLogService _activityLogService;

    public LessonTemplatesController(AppDbContext context, IActivityLogService activityLogService)
    {
        _context = context;
        _activityLogService = activityLogService;
    }

    // GET: LessonTemplates
    public async Task<IActionResult> Index()
    {
        var templates = await _context.LessonTemplates
            .Where(t => t.AktifMi)
            .OrderBy(t => t.Ad)
            .ToListAsync();
        return View(templates);
    }

    // GET: LessonTemplates/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var template = await _context.LessonTemplates
            .FirstOrDefaultAsync(m => m.Id == id);

        if (template == null)
        {
            return NotFound();
        }

        return View(template);
    }

    // GET: LessonTemplates/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: LessonTemplates/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Ad,Konu,VarsayilanSure,VarsayilanUcret,VarsayilanNot,AktifMi")] LessonTemplate template)
    {
        if (ModelState.IsValid)
        {
            template.CreatedAt = DateTime.Now;
            template.AktifMi = true;
            _context.Add(template);
            await _context.SaveChangesAsync();

            // Log activity
            await _activityLogService.LogActivityAsync(
                "Create",
                "LessonTemplate",
                template.Id,
                $"Yeni ders şablonu oluşturuldu: {template.Ad}",
                new { template.Ad, template.Konu, template.VarsayilanSure, template.VarsayilanUcret }
            );

            TempData["SuccessMessage"] = "Ders şablonu başarıyla oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }
        return View(template);
    }

    // GET: LessonTemplates/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var template = await _context.LessonTemplates.FindAsync(id);
        if (template == null)
        {
            return NotFound();
        }
        return View(template);
    }

    // POST: LessonTemplates/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Ad,Konu,VarsayilanSure,VarsayilanUcret,VarsayilanNot,AktifMi,CreatedAt")] LessonTemplate template)
    {
        if (id != template.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                template.UpdatedAt = DateTime.Now;
                _context.Update(template);
                await _context.SaveChangesAsync();

                // Log activity
                await _activityLogService.LogActivityAsync(
                    "Update",
                    "LessonTemplate",
                    template.Id,
                    $"Ders şablonu güncellendi: {template.Ad}",
                    new { template.Ad, template.Konu, template.VarsayilanSure, template.VarsayilanUcret, template.AktifMi }
                );

                TempData["SuccessMessage"] = "Ders şablonu güncellendi.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TemplateExists(template.Id))
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
        return View(template);
    }

    // GET: LessonTemplates/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var template = await _context.LessonTemplates
            .FirstOrDefaultAsync(m => m.Id == id);

        if (template == null)
        {
            return NotFound();
        }

        return View(template);
    }

    // POST: LessonTemplates/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var template = await _context.LessonTemplates.FindAsync(id);
        if (template != null)
        {
            // Soft delete
            template.AktifMi = false;
            template.UpdatedAt = DateTime.Now;
            _context.Update(template);
            await _context.SaveChangesAsync();

            // Log activity
            await _activityLogService.LogActivityAsync(
                "Delete",
                "LessonTemplate",
                template.Id,
                $"Ders şablonu pasif hale getirildi: {template.Ad}",
                new { template.Ad, template.Konu }
            );

            TempData["SuccessMessage"] = "Ders şablonu pasif hale getirildi.";
        }

        return RedirectToAction(nameof(Index));
    }

    // API: Get template by ID (for AJAX)
    [HttpGet]
    public async Task<IActionResult> GetTemplate(int id)
    {
        var template = await _context.LessonTemplates.FindAsync(id);
        if (template == null)
        {
            return NotFound();
        }

        return Json(new
        {
            konu = template.Konu,
            sure = template.VarsayilanSure,
            ucret = template.VarsayilanUcret,
            not = template.VarsayilanNot
        });
    }

    private bool TemplateExists(int id)
    {
        return _context.LessonTemplates.Any(e => e.Id == id);
    }
}
