using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PrivateTutorTracker.Models;

public class Payment
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Öğrenci seçimi zorunludur")]
    [Range(1, int.MaxValue, ErrorMessage = "Lütfen bir öğrenci seçin.")]
    public int StudentId { get; set; }

    [Required(ErrorMessage = "Tarih zorunludur")]
    public DateTime Tarih { get; set; } = DateTime.Now;

    [Required]
    [Range(0, 100000, ErrorMessage = "Tutar 0 ile 100000 arasında olmalıdır")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Tutar { get; set; }

    [Required]
    [StringLength(20)]
    public string Donem { get; set; } = "Haftalık"; // Haftalık, Birikmeli

    [StringLength(500)]
    public string? Aciklama { get; set; }

    [Required]
    [Display(Name = "Ödeme Durumu")]
    public PaymentStatus Durum { get; set; } = PaymentStatus.Beklemede;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    [ForeignKey("StudentId")]
    [ValidateNever]
    public Student? Student { get; set; }
}
