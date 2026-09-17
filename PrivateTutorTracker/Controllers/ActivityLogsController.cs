using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PrivateTutorTracker.Data;
using PrivateTutorTracker.Services;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace PrivateTutorTracker.Controllers
{
    [Authorize]
    public class ActivityLogsController : Controller
    {
        private readonly IActivityLogService _activityLogService;
        private readonly AppDbContext _context;

        public ActivityLogsController(IActivityLogService activityLogService, AppDbContext context)
        {
            _activityLogService = activityLogService;
            _context = context;
        }

        // GET: ActivityLogs
        public async Task<IActionResult> Index(string action = "", string entityType = "", string search = "", DateTime? startDate = null, DateTime? endDate = null, int page = 1, int pageSize = 50)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            // Prepare filter options
            ViewBag.Actions = await _context.ActivityLogs
                .Where(a => a.UserId == currentUserId)
                .Select(a => a.Action)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            ViewBag.EntityTypes = await _context.ActivityLogs
                .Where(a => a.UserId == currentUserId)
                .Select(a => a.EntityType)
                .Distinct()
                .OrderBy(e => e)
                .ToListAsync();

            // Build query
            var query = _context.ActivityLogs
                .Include(a => a.User)
                .Where(a => a.UserId == currentUserId)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(action))
            {
                query = query.Where(a => a.Action == action);
                ViewBag.SelectedAction = action;
            }

            if (!string.IsNullOrEmpty(entityType))
            {
                query = query.Where(a => a.EntityType == entityType);
                ViewBag.SelectedEntityType = entityType;
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(a => a.Description.Contains(search) ||
                                         (a.IpAddress != null && a.IpAddress.Contains(search)) ||
                                         (a.User != null && a.User.UserName != null && a.User.UserName.Contains(search)));
                ViewBag.Search = search;
            }

            if (startDate.HasValue)
            {
                query = query.Where(a => a.CreatedAt >= startDate.Value);
                ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
            }

            if (endDate.HasValue)
            {
                var endDateTime = endDate.Value.AddDays(1).AddSeconds(-1);
                query = query.Where(a => a.CreatedAt <= endDateTime);
                ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
            }

            // Get total count for pagination
            var totalCount = await query.CountAsync();
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            // Get paginated results
            var activityLogs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(activityLogs);
        }

        // GET: ActivityLogs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var activityLog = await _context.ActivityLogs
                .Include(a => a.User)
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier));

            if (activityLog == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(activityLog.Details))
            {
                try
                {
                    using var json = JsonDocument.Parse(activityLog.Details);
                    ViewBag.FormattedDetails = JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
                }
                catch (JsonException)
                {
                    ViewBag.FormattedDetails = activityLog.Details;
                }
            }

            return View(activityLog);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(string activityAction = "", string entityType = "", string search = "", DateTime? startDate = null, DateTime? endDate = null)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var query = _context.ActivityLogs.Include(a => a.User).Where(a => a.UserId == currentUserId);

            if (!string.IsNullOrEmpty(activityAction)) query = query.Where(a => a.Action == activityAction);
            if (!string.IsNullOrEmpty(entityType)) query = query.Where(a => a.EntityType == entityType);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(a => a.Description.Contains(search) || (a.IpAddress != null && a.IpAddress.Contains(search)));
            if (startDate.HasValue) query = query.Where(a => a.CreatedAt >= startDate.Value);
            if (endDate.HasValue) query = query.Where(a => a.CreatedAt < endDate.Value.AddDays(1));

            var logs = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
            static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

            var csv = new StringBuilder("Tarih;Kullanici;Aksiyon;Varlik;VarlikId;Aciklama;IP\r\n");
            foreach (var log in logs)
            {
                csv.AppendLine(string.Join(';',
                    Csv(log.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss")), Csv(log.User?.UserName), Csv(log.Action),
                    Csv(log.EntityType), Csv(log.EntityId?.ToString()), Csv(log.Description), Csv(log.IpAddress)));
            }

            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
                "text/csv", $"AktiviteGunlugu_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }

        // GET: ActivityLogs/Statistics
        public async Task<IActionResult> Statistics(DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!startDate.HasValue)
            {
                startDate = DateTime.Now.AddDays(-30);
            }

            if (!endDate.HasValue)
            {
                endDate = DateTime.Now;
            }

            ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            // Get activity statistics
            var statistics = await _activityLogService.GetActivityStatisticsAsync(startDate, endDate);

            // Get total activities count
            var totalActivities = await _context.ActivityLogs
                .Where(a => a.UserId == currentUserId && a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                .CountAsync();

            ViewBag.TotalActivities = totalActivities;

            // Get most active users
            var mostActiveUsers = await _context.ActivityLogs
                .Where(a => a.UserId == currentUserId && a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                .GroupBy(a => new { a.UserId, a.User!.UserName })
                .Select(g => new { UserName = g.Key.UserName, Count = g.Count() })
                .OrderByDescending(u => u.Count)
                .Take(10)
                .ToListAsync();

            ViewBag.MostActiveUsers = mostActiveUsers;

            // Get activity timeline (daily count)
            var timeline = await _context.ActivityLogs
                .Where(a => a.UserId == currentUserId && a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                .GroupBy(a => a.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(t => t.Date)
                .ToListAsync();

            ViewBag.Timeline = timeline;

            // Get entity type distribution
            var entityDistribution = await _context.ActivityLogs
                .Where(a => a.UserId == currentUserId && a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                .GroupBy(a => a.EntityType)
                .Select(g => new { EntityType = g.Key, Count = g.Count() })
                .OrderByDescending(e => e.Count)
                .ToListAsync();

            ViewBag.EntityDistribution = entityDistribution;

            return View(statistics);
        }

        // GET: ActivityLogs/UserActivity
        public async Task<IActionResult> UserActivity(string? userId = null, int count = 50)
        {
            if (string.IsNullOrEmpty(userId))
            {
                userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            }

            if (string.IsNullOrEmpty(userId))
            {
                return NotFound();
            }

            var userActivities = await _activityLogService.GetUserActivitiesAsync(userId, count);
            var user = await _context.Users.FindAsync(userId);

            ViewBag.UserName = user?.UserName ?? "Unknown";
            ViewBag.Count = count;

            return View(userActivities);
        }

        // GET: ActivityLogs/EntityActivity
        public async Task<IActionResult> EntityActivity(string entityType, int? entityId = null, int count = 50)
        {
            if (string.IsNullOrEmpty(entityType))
            {
                return NotFound();
            }

            var entityActivities = await _activityLogService.GetActivitiesByEntityAsync(entityType, entityId, count);

            ViewBag.EntityType = entityType;
            ViewBag.EntityId = entityId;
            ViewBag.Count = count;

            return View(entityActivities);
        }

        // GET: ActivityLogs/Clear
        public IActionResult Clear()
        {
            return View();
        }

        // POST: ActivityLogs/Clear
        [HttpPost, ActionName("Clear")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearConfirmed(DateTime? olderThan = null)
        {
            try
            {
                var query = _context.ActivityLogs.AsQueryable();
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                query = query.Where(a => a.UserId == currentUserId);

                if (olderThan.HasValue)
                {
                    query = query.Where(a => a.CreatedAt < olderThan.Value);
                }

                var logsToDelete = await query.ToListAsync();
                _context.ActivityLogs.RemoveRange(logsToDelete);
                await _context.SaveChangesAsync();

                // Log this action
                await _activityLogService.LogActivityAsync(
                    "Clear",
                    "ActivityLog",
                    null,
                    $"Cleared {logsToDelete.Count} activity logs" + (olderThan.HasValue ? $" older than {olderThan.Value:yyyy-MM-dd}" : "")
                );

                TempData["SuccessMessage"] = $"{logsToDelete.Count} aktivite kaydı silindi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
                return RedirectToAction(nameof(Clear));
            }
        }
    }
}
