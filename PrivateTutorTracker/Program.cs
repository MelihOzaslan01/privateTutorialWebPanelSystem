using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrivateTutorTracker.Data;
using PrivateTutorTracker.Models;
using PrivateTutorTracker.Services;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Configure for production hosting
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Configure culture
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("tr-TR") };
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("tr-TR");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

// Add DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Her iki ortamda da SQL Server kullan
    options.UseSqlServer(connectionString);
});

// Configure Email Settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

// Configure Google Calendar Service
builder.Services.AddSingleton<GoogleCalendarService>();

// Configure Zoom Services
var zoomSettings = new ZoomS2SSettings();
builder.Configuration.GetSection("Zoom").Bind(zoomSettings);
builder.Services.AddSingleton(zoomSettings);
builder.Services.AddHttpClient(); // HttpClient factory for Zoom services
builder.Services.AddScoped<ZoomAuthService>();
builder.Services.AddScoped<ZoomMeetingService>();

// Configure Lesson Scheduling Service (Zoom + Google Calendar Integration)
builder.Services.AddScoped<LessonSchedulingService>();

// Configure Activity Log Service
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();

// Add Background Services - DISABLED: Otomatik ders tamamlama kapatıldı
// builder.Services.AddHostedService<LessonStatusUpdaterService>();

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 10;
    options.Password.RequiredUniqueChars = 4;

    // Lockout settings
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
        });
}

// Configure cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

var app = builder.Build();

// Run migrations only during an explicitly enabled deployment window.
if (builder.Configuration.GetValue<bool>("DatabaseInitialization:RunMigrations"))
{
    using var migrationScope = app.Services.CreateScope();
    var migrationLogger = migrationScope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseMigration");
    var migrationContext = migrationScope.ServiceProvider.GetRequiredService<AppDbContext>();

    migrationLogger.LogWarning("Database migration was explicitly enabled for this startup.");
    await migrationContext.Database.MigrateAsync();
    migrationLogger.LogInformation("Database migrations completed successfully.");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Security headers for production
app.Use(async (context, next) =>
{
    context.Response.Headers.Remove("X-Powered-By");
    context.Response.Headers.Remove("Server");
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
        "font-src 'self' https://cdn.jsdelivr.net https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();

// Use localization
app.UseRequestLocalization();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

    app.Run();
