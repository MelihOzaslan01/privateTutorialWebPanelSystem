using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateTutorTracker.Data;
using PrivateTutorTracker.Models;
using System.Security.Claims;

namespace PrivateTutorTracker.Controllers;

[Authorize]
public class FinanceController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<FinanceController> _logger;

    public FinanceController(AppDbContext context, ILogger<FinanceController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    }

    // Dashboard - Ana Finans Sayfası
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        var currentMonth = DateTime.Now.Month;
        var currentYear = DateTime.Now.Year;

        // Bu ay gelirleri
        var monthlyIncomes = await _context.Incomes
            .Where(i => i.UserId == userId
                     && i.Tarih.Month == currentMonth
                     && i.Tarih.Year == currentYear)
            .ToListAsync();

        ViewBag.MonthlyIncomeTotal = monthlyIncomes.Sum(i => i.Tutar);
        ViewBag.MonthlyIncomeCount = monthlyIncomes.Count;

        // Bu ay giderleri
        var monthlyExpenses = await _context.Expenses
            .Where(e => e.UserId == userId
                     && e.Tarih.Month == currentMonth
                     && e.Tarih.Year == currentYear)
            .ToListAsync();

        ViewBag.MonthlyExpenseTotal = monthlyExpenses.Sum(e => e.Tutar);
        ViewBag.MonthlyExpenseCount = monthlyExpenses.Count;

        // Aktif borçlar
        var activeDebts = await _context.Debts
            .Where(d => d.UserId == userId && d.AktifMi)
            .ToListAsync();

        ViewBag.TotalDebt = activeDebts.Sum(d => d.KalanTutar);
        ViewBag.MonthlyDebtPayment = activeDebts.Sum(d => d.AylikTaksit);
        ViewBag.ActiveDebtCount = activeDebts.Count;

        // Yatırımlar
        var investments = await _context.Investments
            .Where(i => i.UserId == userId && i.AktifMi)
            .ToListAsync();

        ViewBag.TotalInvestmentValue = investments.Sum(i => i.ToplamDeger);
        ViewBag.TotalInvestmentProfit = investments.Sum(i => i.Kar);
        ViewBag.InvestmentCount = investments.Count;

        // Net Durum
        var totalIncome = ViewBag.MonthlyIncomeTotal;
        var totalExpense = ViewBag.MonthlyExpenseTotal;
        var totalDebtPayment = ViewBag.MonthlyDebtPayment;

        ViewBag.NetBalance = totalIncome - totalExpense - totalDebtPayment;
        ViewBag.ExpenseRatio = totalIncome > 0 ? (int)((totalExpense / totalIncome) * 100) : 0;

        // Gider kategorileri (Pie Chart için)
        var expensesByCategory = monthlyExpenses
            .GroupBy(e => e.Kategori)
            .Select(g => new { Kategori = g.Key, Tutar = g.Sum(e => e.Tutar) })
            .ToList();

        ViewBag.ExpenseCategoryLabels = System.Text.Json.JsonSerializer.Serialize(expensesByCategory.Select(x => x.Kategori).ToList());
        ViewBag.ExpenseCategoryData = System.Text.Json.JsonSerializer.Serialize(expensesByCategory.Select(x => x.Tutar).ToList());

        // Son 6 ay gelir-gider trendi
        var last6Months = Enumerable.Range(0, 6)
            .Select(i => DateTime.Now.AddMonths(-5 + i))
            .ToList();

        var monthlyTrend = new List<object>();
        var monthLabels = new List<string>();

        foreach (var month in last6Months)
        {
            var incomeTotal = await _context.Incomes
                .Where(i => i.UserId == userId
                         && i.Tarih.Month == month.Month
                         && i.Tarih.Year == month.Year)
                .SumAsync(i => i.Tutar);

            var expenseTotal = await _context.Expenses
                .Where(e => e.UserId == userId
                         && e.Tarih.Month == month.Month
                         && e.Tarih.Year == month.Year)
                .SumAsync(e => e.Tutar);

            monthLabels.Add(month.ToString("MMMM"));
            monthlyTrend.Add(new { Income = incomeTotal, Expense = expenseTotal });
        }

        ViewBag.MonthLabels = System.Text.Json.JsonSerializer.Serialize(monthLabels);
        ViewBag.IncomeData = System.Text.Json.JsonSerializer.Serialize(monthlyTrend.Select(x => ((dynamic)x).Income).ToList());
        ViewBag.ExpenseData = System.Text.Json.JsonSerializer.Serialize(monthlyTrend.Select(x => ((dynamic)x).Expense).ToList());

        // Son işlemler
        var recentIncomes = await _context.Incomes
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.Tarih)
            .Take(5)
            .ToListAsync();

        var recentExpenses = await _context.Expenses
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Tarih)
            .Take(5)
            .ToListAsync();

        ViewBag.RecentIncomes = recentIncomes;
        ViewBag.RecentExpenses = recentExpenses;
        ViewBag.RecentDebts = activeDebts.OrderBy(d => d.SonrakiOdemeTarihi).Take(5).ToList();
        ViewBag.Investments = investments;

        return View();
    }

    // Gelir Yönetimi
    public async Task<IActionResult> Incomes()
    {
        var userId = GetCurrentUserId();
        var incomes = await _context.Incomes
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.Tarih)
            .ToListAsync();

        return View(incomes);
    }

    // Gider Yönetimi
    public async Task<IActionResult> Expenses()
    {
        var userId = GetCurrentUserId();
        var expenses = await _context.Expenses
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Tarih)
            .ToListAsync();

        return View(expenses);
    }

    // Borç Yönetimi
    public async Task<IActionResult> Debts()
    {
        var userId = GetCurrentUserId();
        var debts = await _context.Debts
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return View(debts);
    }

    // Yatırım Yönetimi
    public async Task<IActionResult> Investments()
    {
        var userId = GetCurrentUserId();
        var investments = await _context.Investments
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return View(investments);
    }

    // CRUD Actions - Income
    [HttpGet]
    public IActionResult CreateIncome()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateIncome(Income income)
    {
        // UserId validasyonunu kaldır çünkü controller'da otomatik doldurulacak
        ModelState.Remove("UserId");

        if (ModelState.IsValid)
        {
            income.UserId = GetCurrentUserId();
            income.CreatedAt = DateTime.Now;

            _context.Incomes.Add(income);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Gelir başarıyla eklendi.";
            return RedirectToAction(nameof(Incomes));
        }

        return View(income);
    }

    [HttpGet]
    public async Task<IActionResult> EditIncome(int id)
    {
        var userId = GetCurrentUserId();
        var income = await _context.Incomes.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

        if (income == null)
        {
            return NotFound();
        }

        return View(income);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditIncome(int id, Income income)
    {
        if (id != income.Id)
        {
            return NotFound();
        }

        var userId = GetCurrentUserId();
        var existingIncome = await _context.Incomes.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

        if (existingIncome == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            existingIncome.Ad = income.Ad;
            existingIncome.Tutar = income.Tutar;
            existingIncome.Tarih = income.Tarih;
            existingIncome.Siklik = income.Siklik;
            existingIncome.Kategori = income.Kategori;
            existingIncome.Aciklama = income.Aciklama;
            existingIncome.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Gelir başarıyla güncellendi.";
            return RedirectToAction(nameof(Incomes));
        }

        return View(income);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteIncome(int id)
    {
        var userId = GetCurrentUserId();
        var income = await _context.Incomes.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

        if (income == null)
        {
            return NotFound();
        }

        _context.Incomes.Remove(income);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Gelir başarıyla silindi.";
        return RedirectToAction(nameof(Incomes));
    }

    // CRUD Actions - Expense
    [HttpGet]
    public IActionResult CreateExpense()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExpense(Expense expense)
    {
        // UserId validasyonunu kaldır çünkü controller'da otomatik doldurulacak
        ModelState.Remove("UserId");

        if (ModelState.IsValid)
        {
            expense.UserId = GetCurrentUserId();
            expense.CreatedAt = DateTime.Now;

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Gider başarıyla eklendi.";
            return RedirectToAction(nameof(Expenses));
        }

        return View(expense);
    }

    [HttpGet]
    public async Task<IActionResult> EditExpense(int id)
    {
        var userId = GetCurrentUserId();
        var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (expense == null)
        {
            return NotFound();
        }

        return View(expense);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditExpense(int id, Expense expense)
    {
        if (id != expense.Id)
        {
            return NotFound();
        }

        var userId = GetCurrentUserId();
        var existingExpense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (existingExpense == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            existingExpense.Ad = expense.Ad;
            existingExpense.Tutar = expense.Tutar;
            existingExpense.Tarih = expense.Tarih;
            existingExpense.Kategori = expense.Kategori;
            existingExpense.OdemeYontemi = expense.OdemeYontemi;
            existingExpense.Aciklama = expense.Aciklama;
            existingExpense.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Gider başarıyla güncellendi.";
            return RedirectToAction(nameof(Expenses));
        }

        return View(expense);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        var userId = GetCurrentUserId();
        var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (expense == null)
        {
            return NotFound();
        }

        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Gider başarıyla silindi.";
        return RedirectToAction(nameof(Expenses));
    }

    // CRUD Actions - Debt
    [HttpGet]
    public IActionResult CreateDebt()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDebt(Debt debt)
    {
        // UserId validasyonunu kaldır çünkü controller'da otomatik doldurulacak
        ModelState.Remove("UserId");

        if (ModelState.IsValid)
        {
            debt.UserId = GetCurrentUserId();
            debt.CreatedAt = DateTime.Now;

            // Eğer KalanTutar girilmediyse veya 0 ise, ToplamTutar'ı kullan
            if (debt.KalanTutar == 0)
            {
                debt.KalanTutar = debt.ToplamTutar;
            }

            _context.Debts.Add(debt);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Borç başarıyla eklendi.";
            return RedirectToAction(nameof(Debts));
        }

        return View(debt);
    }

    [HttpGet]
    public async Task<IActionResult> EditDebt(int id)
    {
        var userId = GetCurrentUserId();
        var debt = await _context.Debts.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

        if (debt == null)
        {
            return NotFound();
        }

        return View(debt);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDebt(int id, Debt debt)
    {
        if (id != debt.Id)
        {
            return NotFound();
        }

        var userId = GetCurrentUserId();
        var existingDebt = await _context.Debts.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

        if (existingDebt == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            existingDebt.Ad = debt.Ad;
            existingDebt.ToplamTutar = debt.ToplamTutar;
            existingDebt.KalanTutar = debt.KalanTutar;
            existingDebt.AylikTaksit = debt.AylikTaksit;
            existingDebt.FaizOrani = debt.FaizOrani;
            existingDebt.ToplamTaksitSayisi = debt.ToplamTaksitSayisi;
            existingDebt.KalanTaksitSayisi = debt.KalanTaksitSayisi;
            existingDebt.BaslangicTarihi = debt.BaslangicTarihi;
            existingDebt.SonOdemeTarihi = debt.SonOdemeTarihi;
            existingDebt.SonrakiOdemeTarihi = debt.SonrakiOdemeTarihi;
            existingDebt.BorcTipi = debt.BorcTipi;
            existingDebt.KurumKisi = debt.KurumKisi;
            existingDebt.Aciklama = debt.Aciklama;
            existingDebt.AktifMi = debt.AktifMi;
            existingDebt.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Borç başarıyla güncellendi.";
            return RedirectToAction(nameof(Debts));
        }

        return View(debt);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDebt(int id)
    {
        var userId = GetCurrentUserId();
        var debt = await _context.Debts.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

        if (debt == null)
        {
            return NotFound();
        }

        _context.Debts.Remove(debt);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Borç başarıyla silindi.";
        return RedirectToAction(nameof(Debts));
    }

    // CRUD Actions - Investment
    [HttpGet]
    public IActionResult CreateInvestment()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInvestment(Investment investment)
    {
        // UserId validasyonunu kaldır çünkü controller'da otomatik doldurulacak
        ModelState.Remove("UserId");

        if (ModelState.IsValid)
        {
            investment.UserId = GetCurrentUserId();
            investment.CreatedAt = DateTime.Now;

            // Eğer güncel fiyat girilmediyse, alış fiyatını kullan
            if (investment.GuncelFiyat == 0)
            {
                investment.GuncelFiyat = investment.BirimFiyat;
            }

            investment.SonGuncelleme = DateTime.Now;

            _context.Investments.Add(investment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Yatırım başarıyla eklendi.";
            return RedirectToAction(nameof(Investments));
        }

        return View(investment);
    }

    [HttpGet]
    public async Task<IActionResult> EditInvestment(int id)
    {
        var userId = GetCurrentUserId();
        var investment = await _context.Investments.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

        if (investment == null)
        {
            return NotFound();
        }

        return View(investment);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditInvestment(int id, Investment investment)
    {
        if (id != investment.Id)
        {
            return NotFound();
        }

        var userId = GetCurrentUserId();
        var existingInvestment = await _context.Investments.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

        if (existingInvestment == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            existingInvestment.Ad = investment.Ad;
            existingInvestment.Tip = investment.Tip;
            existingInvestment.Miktar = investment.Miktar;
            existingInvestment.BirimFiyat = investment.BirimFiyat;
            existingInvestment.GuncelFiyat = investment.GuncelFiyat;
            existingInvestment.AlisTarihi = investment.AlisTarihi;
            existingInvestment.Aciklama = investment.Aciklama;
            existingInvestment.AktifMi = investment.AktifMi;
            existingInvestment.UpdatedAt = DateTime.Now;
            existingInvestment.SonGuncelleme = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Yatırım başarıyla güncellendi.";
            return RedirectToAction(nameof(Investments));
        }

        return View(investment);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInvestment(int id)
    {
        var userId = GetCurrentUserId();
        var investment = await _context.Investments.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

        if (investment == null)
        {
            return NotFound();
        }

        _context.Investments.Remove(investment);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Yatırım başarıyla silindi.";
        return RedirectToAction(nameof(Investments));
    }
}
