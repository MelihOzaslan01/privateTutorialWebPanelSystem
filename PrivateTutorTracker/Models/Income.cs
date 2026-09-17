using System.ComponentModel.DataAnnotations;

namespace PrivateTutorTracker.Models;

public class Income
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Gelir adı zorunludur")]
    [StringLength(100)]
    [Display(Name = "Gelir Adı")]
    public string Ad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tutar zorunludur")]
    [Display(Name = "Tutar")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Tutar 0'dan büyük olmalıdır")]
    public decimal Tutar { get; set; }

    [Required(ErrorMessage = "Tarih zorunludur")]
    [Display(Name = "Tarih")]
    public DateTime Tarih { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Sıklık zorunludur")]
    [StringLength(20)]
    [Display(Name = "Sıklık")]
    public string Siklik { get; set; } = "Bir Kerelik"; // Bir Kerelik, Aylık, Haftalık, Yıllık

    [StringLength(500)]
    [Display(Name = "Açıklama")]
    public string? Aciklama { get; set; }

    [Required(ErrorMessage = "Kategori zorunludur")]
    [Display(Name = "Kategori")]
    [StringLength(50)]
    public string Kategori { get; set; } = "Diğer"; // Maaş, Ek İş, Kira Geliri, Yatırım Geliri, Diğer

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    [Required]
    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }
}
