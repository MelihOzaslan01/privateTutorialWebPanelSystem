using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using PrivateTutorTracker.Data;
using PrivateTutorTracker.Models;
using PrivateTutorTracker.Models.ViewModels;
using PrivateTutorTracker.Services;

namespace PrivateTutorTracker.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;
    private readonly ILogger<AccountController> _logger;
    private readonly IActivityLogService _activityLogService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        ILogger<AccountController> logger,
        IActivityLogService activityLogService,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _context = context;
        _logger = logger;
        _activityLogService = activityLogService;
        _configuration = configuration;
        _environment = environment;
    }

    // GET: Account/Login
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["GoogleLoginEnabled"] = IsGoogleLoginEnabled();

        if (TempData["LoginError"] is string loginError)
        {
            ModelState.AddModelError(string.Empty, loginError);
        }

        return View();
    }

    // POST: Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(
                model.Username,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                // Log activity
                await _activityLogService.LogActivityAsync(
                    "Login",
                    "User",
                    null,
                    $"Kullanıcı giriş yaptı: {model.Username}",
                    new { Username = model.Username }
                );

                // ReturnUrl doğrulaması - çok uzun URL'leri ve döngüleri engelle
                if (!string.IsNullOrEmpty(returnUrl) &&
                    returnUrl.Length < 200 &&
                    Url.IsLocalUrl(returnUrl) &&
                    !returnUrl.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase) &&
                    !returnUrl.Contains("ReturnUrl", StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Hesabınız kilitlendi. Lütfen daha sonra tekrar deneyin.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Kullanıcı adı veya şifre hatalı.");
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult GoogleLogin(string? returnUrl = null)
    {
        if (!IsGoogleLoginEnabled())
        {
            TempData["LoginError"] = "Google ile giriş henüz yapılandırılmamış.";
            return RedirectToAction(nameof(Login));
        }

        var safeReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Index", "Home");
        var redirectUrl = Url.Action(nameof(GoogleLoginCallback), "Account", new { returnUrl = safeReturnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
        return Challenge(properties, "Google");
    }

    [HttpGet]
    public async Task<IActionResult> GoogleLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (!string.IsNullOrWhiteSpace(remoteError))
        {
            _logger.LogWarning("Google login returned an error: {RemoteError}", remoteError);
            TempData["LoginError"] = "Google ile giriş tamamlanamadı.";
            return RedirectToAction(nameof(Login));
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            TempData["LoginError"] = "Google oturum bilgileri alınamadı.";
            return RedirectToAction(nameof(Login));
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email) || !IsAllowedGoogleEmail(email))
        {
            _logger.LogWarning("Unauthorized Google login attempt for {Email}", email ?? "unknown");
            TempData["LoginError"] = "Bu Google hesabının panele giriş yetkisi yok.";
            return RedirectToAction(nameof(Login));
        }

        var externalResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: false);

        if (externalResult.Succeeded)
        {
            return LocalRedirectOrHome(returnUrl);
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            var linkedUsername = _configuration["Authentication:Google:LinkedUsername"];
            if (!string.IsNullOrWhiteSpace(linkedUsername))
            {
                user = await _userManager.FindByNameAsync(linkedUsername.Trim());
            }
        }

        if (user == null)
        {
            TempData["LoginError"] = "Bu e-posta ile eşleşen mevcut bir panel hesabı bulunamadı.";
            return RedirectToAction(nameof(Login));
        }

        var addLoginResult = await _userManager.AddLoginAsync(user, info);
        if (!addLoginResult.Succeeded)
        {
            _logger.LogWarning("Google login could not be linked to user {UserId}: {Errors}",
                user.Id, string.Join(", ", addLoginResult.Errors.Select(error => error.Code)));
            TempData["LoginError"] = "Google hesabı panel hesabına bağlanamadı.";
            return RedirectToAction(nameof(Login));
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        await _activityLogService.LogActivityAsync(
            "Login",
            "User",
            null,
            $"Google ile kullanıcı girişi yapıldı: {user.UserName}",
            new { user.UserName, Email = email });

        return LocalRedirectOrHome(returnUrl);
    }

    private bool IsGoogleLoginEnabled()
    {
        return !string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientId"]) &&
               !string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientSecret"]);
    }

    private bool IsAllowedGoogleEmail(string email)
    {
        var allowedEmails = _configuration.GetSection("Authentication:Google:AllowedEmails").Get<string[]>()
            ?? Array.Empty<string>();

        return allowedEmails.Any(allowedEmail =>
            string.Equals(allowedEmail.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private IActionResult LocalRedirectOrHome(string? returnUrl)
    {
        return Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Home");
    }

    // POST: Account/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        // Log activity before signing out
        var username = User.Identity?.Name ?? "Unknown";
        await _activityLogService.LogActivityAsync(
            "Logout",
            "User",
            null,
            $"Kullanıcı çıkış yaptı: {username}",
            new { Username = username }
        );

        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // GET: Account/AccessDenied
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // GET: Account/Profile
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction(nameof(Login));
        }

        var model = new ProfileViewModel
        {
            Username = user.UserName ?? "",
            FullName = user.FullName ?? "",
            Email = user.Email ?? ""
        };

        return View(model);
    }

    // POST: Account/Profile
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction(nameof(Login));
        }

        user.FullName = model.FullName;
        user.Email = model.Email;

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            // Log activity
            await _activityLogService.LogActivityAsync(
                "Update",
                "UserProfile",
                null,
                $"Profil güncellendi: {user.UserName}",
                new { user.UserName, model.FullName, model.Email }
            );

            TempData["SuccessMessage"] = "Profil bilgileriniz başarıyla güncellendi.";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    // GET: Account/ChangePassword
    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword()
    {
        return View();
    }

    // POST: Account/ChangePassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction(nameof(Login));
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);

            // Log activity
            await _activityLogService.LogActivityAsync(
                "Update",
                "UserPassword",
                null,
                $"Şifre değiştirildi: {user.UserName}",
                new { user.UserName }
            );

            TempData["SuccessMessage"] = "Şifreniz başarıyla değiştirildi.";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    // GET: Account/InitializeDatabase - Manuel migration ve admin oluşturma
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> InitializeDatabase()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            var messages = new List<string>();

            // 1. Veritabanı bağlantısını test et
            _logger.LogInformation("Testing database connection...");
            if (!_context.Database.CanConnect())
            {
                return Content("HATA: Veritabanına bağlanılamıyor. Connection string'i kontrol edin.");
            }
            messages.Add("✓ Veritabanı bağlantısı başarılı.");

            // 2. Migration'ları uygula
            _logger.LogInformation("Applying database migrations...");
            await _context.Database.MigrateAsync();
            messages.Add("✓ Veritabanı migration'ları tamamlandı.");

            // 3. Admin kullanıcısı var mı kontrol et
            var existingAdmin = await _userManager.FindByNameAsync("admin");
            if (existingAdmin != null)
            {
                messages.Add("⚠ Admin kullanıcısı zaten mevcut.");
            }
            else
            {
                // Admin kullanıcısı oluştur
                var adminUser = new ApplicationUser
                {
                    UserName = "admin",
                    Email = "admin@privatetutortracker.com",
                    EmailConfirmed = true,
                    FullName = "Sistem Yöneticisi"
                };

                var result = await _userManager.CreateAsync(adminUser, "admin");

                if (result.Succeeded)
                {
                    messages.Add("✓ Admin kullanıcısı oluşturuldu!");
                    messages.Add("  Kullanıcı Adı: admin");
                    messages.Add("  Şifre: admin");
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    messages.Add($"✗ Admin kullanıcısı oluşturulamadı: {errors}");
                }
            }

            // 4. Demo kullanıcısı var mı kontrol et
            var existingDemo = await _userManager.FindByNameAsync("demo");
            if (existingDemo != null)
            {
                messages.Add("⚠ Demo kullanıcısı zaten mevcut.");
            }
            else
            {
                // Demo kullanıcısı oluştur
                var demoUser = new ApplicationUser
                {
                    UserName = "demo",
                    Email = "demo@privatetutortracker.com",
                    EmailConfirmed = true,
                    FullName = "Demo Kullanıcı"
                };

                var result = await _userManager.CreateAsync(demoUser, "demo");

                if (result.Succeeded)
                {
                    messages.Add("✓ Demo kullanıcısı oluşturuldu!");
                    messages.Add("  Kullanıcı Adı: demo");
                    messages.Add("  Şifre: demo");
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    messages.Add($"✗ Demo kullanıcısı oluşturulamadı: {errors}");
                }
            }

            messages.Add("");
            messages.Add("=== Veritabanı Başlatma Tamamlandı ===");
            messages.Add("Şimdi giriş yapabilirsiniz: /Account/Login");

            return Content(string.Join("\n", messages), "text/plain");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database initialization failed");
            return Content($"HATA: {ex.Message}\n\nDetay: {ex.InnerException?.Message}", "text/plain");
        }
    }
}
