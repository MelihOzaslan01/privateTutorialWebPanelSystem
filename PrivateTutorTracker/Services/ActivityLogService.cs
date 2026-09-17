using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PrivateTutorTracker.Data;
using PrivateTutorTracker.Models;

namespace PrivateTutorTracker.Services;

public interface IActivityLogService
{
    Task LogActivityAsync(string action, string entityType, int? entityId, string description, object? details = null);
    Task<List<ActivityLog>> GetUserActivitiesAsync(string userId, int count = 50);
    Task<List<ActivityLog>> GetActivitiesByEntityAsync(string entityType, int? entityId = null, int count = 50);
    Task<Dictionary<string, int>> GetActivityStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);
}

public class ActivityLogService : IActivityLogService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(
        AppDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ActivityLogService> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogActivityAsync(string action, string entityType, int? entityId, string description, object? details = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var userId = httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogDebug("Activity log skipped because there is no authenticated user. Action: {Action}", action);
                return;
            }

            var log = new ActivityLog
            {
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Description = description,
                Details = details == null ? null : JsonSerializer.Serialize(details),
                CreatedAt = DateTime.Now,
                IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = httpContext?.Request.Headers.UserAgent.ToString()
            };

            _context.ActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Activity log could not be saved.");
        }
    }

    public Task<List<ActivityLog>> GetUserActivitiesAsync(string userId, int count = 50)
    {
        return _context.ActivityLogs
            .Include(a => a.User)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public Task<List<ActivityLog>> GetActivitiesByEntityAsync(string entityType, int? entityId = null, int count = 50)
    {
        var query = _context.ActivityLogs
            .Include(a => a.User)
            .Where(a => a.EntityType == entityType);

        if (entityId.HasValue)
        {
            query = query.Where(a => a.EntityId == entityId.Value);
        }

        return query
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetActivityStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.ActivityLogs.AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= endDate.Value);
        }

        return await query
            .GroupBy(a => a.Action)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToDictionaryAsync(x => x.Action, x => x.Count);
    }
}
