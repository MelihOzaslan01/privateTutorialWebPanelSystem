using System.ComponentModel.DataAnnotations;

namespace PrivateTutorTracker.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Kullanici adi zorunludur.")]
    [Display(Name = "Kullanici Adi")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sifre zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Sifre")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Beni Hatirla")]
    public bool RememberMe { get; set; }
}

public class ProfileViewModel
{
    [Display(Name = "Kullanici Adi")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ad soyad zorunludur.")]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email zorunludur.")]
    [EmailAddress(ErrorMessage = "Gecerli bir email adresi giriniz.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Mevcut sifre zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut Sifre")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni sifre zorunludur.")]
    [DataType(DataType.Password)]
    [MinLength(10, ErrorMessage = "Sifre en az 10 karakter olmalidir.")]
    [Display(Name = "Yeni Sifre")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni sifre tekrari zorunludur.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Yeni sifreler eslesmiyor.")]
    [Display(Name = "Yeni Sifre Tekrar")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
