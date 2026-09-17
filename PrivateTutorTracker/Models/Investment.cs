using System.ComponentModel.DataAnnotations;

namespace PrivateTutorTracker.Models;

public class Investment
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Yatırım adı zorunludur")]
    [StringLength(100)]
    [Display(Name = "Yatırım Adı")]
    public string Ad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yatırım tipi zorunludur")]
    [StringLength(50)]
    [Display(Name = "Yatırım Tipi")]
    public string Tip { get; set; } = "Diğer"; // Altın, Döviz, Hisse Senedi, Kripto Para, Diğer

    [Required(ErrorMessage = "Miktar zorunludur")]
    [Display(Name = "Miktar")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Miktar 0'dan büyük olmalıdır")]
    public decimal Miktar { get; set; }

    [Required(ErrorMessage = "Birim fiyat zorunludur")]
    [Display(Name = "Birim Fiyat")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Birim fiyat 0'dan büyük olmalıdır")]
    public decimal BirimFiyat { get; set; }

    [Display(Name = "Güncel Fiyat")]
    [Range(0, double.MaxValue, ErrorMessage = "Güncel fiyat 0'dan küçük olamaz")]
    public decimal GuncelFiyat { get; set; }

    [Required(ErrorMessage = "Alış tarihi zorunludur")]
    [Display(Name = "Alış Tarihi")]
    public DateTime AlisTarihi { get; set; } = DateTime.Now;

    [Display(Name = "Son Güncelleme")]
    public DateTime? SonGuncelleme { get; set; }

    [StringLength(500)]
    [Display(Name = "Açıklama")]
    public string? Aciklama { get; set; }

    [Display(Name = "Aktif")]
    public bool AktifMi { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    [Required]
    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    // Hesaplanan özellikler
    public decimal ToplamDeger => Miktar * GuncelFiyat;
    public decimal Kar => (GuncelFiyat - BirimFiyat) * Miktar;
    public decimal KarYuzdesi => BirimFiyat > 0 ? ((GuncelFiyat - BirimFiyat) / BirimFiyat) * 100 : 0;
}
