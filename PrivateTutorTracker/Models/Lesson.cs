using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PrivateTutorTracker.Models;

public class Lesson
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Öğrenci seçimi zorunludur.")]
    [Range(1, int.MaxValue, ErrorMessage = "Lütfen bir öğrenci seçin.")]
    [Display(Name = "Öğrenci")]
    public int StudentId { get; set; }

    [Required(ErrorMessage = "Başlangıç saati zorunludur")]
    public DateTime Baslangic { get; set; }

    [Required(ErrorMessage = "Bitiş saati zorunludur")]
    public DateTime Bitis { get; set; }

    [Required(ErrorMessage = "Konu alanı zorunludur")]
    [StringLength(200)]
    public string Konu { get; set; } = string.Empty;

    [Required]
    [Range(0, 10000, ErrorMessage = "Ücret 0 ile 10000 arasında olmalıdır")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Ucret { get; set; }

    [Required]
    [StringLength(20)]
    public string Durum { get; set; } = "Planlı"; // Planlı, Tamamlandı, İptal

    [StringLength(500)]
    public string? Not { get; set; }

    public bool OdemeAlindi { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }

    // Google Calendar Integration
    [StringLength(100)]
    public string? GoogleCalendarEventId { get; set; }

    // Zoom Integration
    public long? ZoomMeetingId { get; set; }

    // Recurring Lesson Support
    public bool TekrarliMi { get; set; } = false;

    [StringLength(20)]
    public string? TekrarPeriyodu { get; set; } // Günlük, Haftalık, İkiHaftadaBir

    public int? TekrarGrupId { get; set; } // Groups recurring lessons together

    public int? TekrarSirasi { get; set; } // Order in recurring series (1, 2, 3, etc.)

    // Lesson Type and Notes
    [Required]
    [Display(Name = "Ders Türü")]
    public LessonType Tur { get; set; } = LessonType.YuzYuze;

    [StringLength(1000)]
    [Display(Name = "Ders Notu")]
    public string? DersNotu { get; set; }

    // Navigation property
    [ForeignKey("StudentId")]
    [ValidateNever]
    public Student? Student { get; set; }

    // Computed properties
    public int Sure => (int)(Bitis - Baslangic).TotalMinutes;
}
