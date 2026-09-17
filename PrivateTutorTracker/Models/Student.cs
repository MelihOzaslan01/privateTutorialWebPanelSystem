using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PrivateTutorTracker.Models;

public class Student
{
    public int Id { get; set; }

    // User ownership
    [Required]
    public string UserId { get; set; } = string.Empty;

    [ValidateNever]
    public ApplicationUser? User { get; set; }

    [Required(ErrorMessage = "Ad alanı zorunludur")]
    [StringLength(50)]
    public string Ad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Soyad alanı zorunludur")]
    [StringLength(50)]
    public string Soyad { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz")]
    [StringLength(100)]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz")]
    [StringLength(20)]
    public string? Telefon { get; set; }

    [StringLength(500)]
    public string? Not { get; set; }

    public bool AktifMi { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    // Computed property
    public string AdSoyad => $"{Ad} {Soyad}";
}
