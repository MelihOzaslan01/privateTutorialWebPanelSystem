using System.ComponentModel.DataAnnotations;

namespace PrivateTutorTracker.Models;

public class Expense
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Gider adı zorunludur")]
    [StringLength(100)]
    [Display(Name = "Gider Adı")]
    public string Ad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tutar zorunludur")]
    [Display(Name = "Tutar")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Tutar 0'dan büyük olmalıdır")]
    public decimal Tutar { get; set; }

    [Required(ErrorMessage = "Tarih zorunludur")]
    [Display(Name = "Tarih")]
    public DateTime Tarih { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Kategori zorunludur")]
    [StringLength(50)]
    [Display(Name = "Kategori")]
    public string Kategori { get; set; } = "Diğer"; // Market, Fatura, Ulaşım, Eğlence, Sağlık, Diğer

    [Required(ErrorMessage = "Ödeme yöntemi zorunludur")]
    [StringLength(50)]
    [Display(Name = "Ödeme Yöntemi")]
    public string OdemeYontemi { get; set; } = "Nakit"; // Nakit, Kredi Kartı, Banka Kartı

    [StringLength(500)]
    [Display(Name = "Açıklama")]
    public string? Aciklama { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    [Required]
    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }
}
