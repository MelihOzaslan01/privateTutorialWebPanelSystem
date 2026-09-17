using System.ComponentModel.DataAnnotations;

namespace PrivateTutorTracker.Models;

public class LessonTemplate
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Şablon adı zorunludur")]
    [StringLength(100)]
    [Display(Name = "Şablon Adı")]
    public string Ad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Konu alanı zorunludur")]
    [StringLength(200)]
    [Display(Name = "Konu")]
    public string Konu { get; set; } = string.Empty;

    [Required(ErrorMessage = "Varsayılan süre zorunludur")]
    [Range(10, 240, ErrorMessage = "Süre 10 ile 240 dakika arasında olmalıdır")]
    [Display(Name = "Varsayılan Süre (dakika)")]
    public int VarsayilanSure { get; set; } = 40;

    [Required(ErrorMessage = "Varsayılan ücret zorunludur")]
    [Range(0, 10000, ErrorMessage = "Ücret 0 ile 10000 arasında olmalıdır")]
    [Display(Name = "Varsayılan Ücret")]
    public decimal VarsayilanUcret { get; set; } = 500m;

    [StringLength(500)]
    [Display(Name = "Varsayılan Not")]
    public string? VarsayilanNot { get; set; }

    [Display(Name = "Aktif Mi")]
    public bool AktifMi { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }
}
