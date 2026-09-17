using System.ComponentModel.DataAnnotations;

namespace PrivateTutorTracker.Models;

public class Debt
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Borç adı zorunludur")]
    [StringLength(100)]
    [Display(Name = "Borç Adı")]
    public string Ad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Toplam tutar zorunludur")]
    [Display(Name = "Toplam Tutar")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Tutar 0'dan büyük olmalıdır")]
    public decimal ToplamTutar { get; set; }

    [Display(Name = "Kalan Tutar")]
    [Range(0, double.MaxValue, ErrorMessage = "Kalan tutar 0'dan küçük olamaz")]
    public decimal KalanTutar { get; set; }

    [Required(ErrorMessage = "Aylık taksit zorunludur")]
    [Display(Name = "Aylık Taksit")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Aylık taksit 0'dan büyük olmalıdır")]
    public decimal AylikTaksit { get; set; }

    [Display(Name = "Faiz Oranı (%)")]
    [Range(0, 100, ErrorMessage = "Faiz oranı 0-100 arasında olmalıdır")]
    public decimal FaizOrani { get; set; } = 0;

    [Required(ErrorMessage = "Toplam taksit sayısı zorunludur")]
    [Display(Name = "Toplam Taksit Sayısı")]
    [Range(1, int.MaxValue, ErrorMessage = "Toplam taksit sayısı en az 1 olmalıdır")]
    public int ToplamTaksitSayisi { get; set; }

    [Required(ErrorMessage = "Kalan taksit sayısı zorunludur")]
    [Display(Name = "Kalan Taksit Sayısı")]
    [Range(0, int.MaxValue, ErrorMessage = "Kalan taksit sayısı 0'dan küçük olamaz")]
    public int KalanTaksitSayisi { get; set; }

    [Required(ErrorMessage = "Başlangıç tarihi zorunludur")]
    [Display(Name = "Başlangıç Tarihi")]
    public DateTime BaslangicTarihi { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Son ödeme tarihi zorunludur")]
    [Display(Name = "Son Ödeme Tarihi")]
    public DateTime SonOdemeTarihi { get; set; } = DateTime.Now.AddYears(1);

    [Required(ErrorMessage = "Sonraki ödeme tarihi zorunludur")]
    [Display(Name = "Bir Sonraki Ödeme Tarihi")]
    public DateTime SonrakiOdemeTarihi { get; set; } = DateTime.Now.AddMonths(1);

    [Required(ErrorMessage = "Borç tipi zorunludur")]
    [StringLength(50)]
    [Display(Name = "Borç Tipi")]
    public string BorcTipi { get; set; } = "Diğer"; // Kredi Kartı, Konut Kredisi, Araç Kredisi, İhtiyaç Kredisi, Bireysel Borç, Diğer

    [StringLength(100)]
    [Display(Name = "Kurum/Kişi")]
    public string? KurumKisi { get; set; }

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
}
