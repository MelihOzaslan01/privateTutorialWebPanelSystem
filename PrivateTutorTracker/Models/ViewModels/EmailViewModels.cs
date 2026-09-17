using System.ComponentModel.DataAnnotations;

namespace PrivateTutorTracker.Models.ViewModels;

public class SendEmailViewModel
{
    public int StudentId { get; set; }

    [Display(Name = "Ogrenci")]
    public string StudentName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Alici email adresi zorunludur.")]
    [EmailAddress(ErrorMessage = "Gecerli bir email adresi giriniz.")]
    [Display(Name = "Alici")]
    public string ToEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Konu zorunludur.")]
    [Display(Name = "Konu")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mesaj zorunludur.")]
    [Display(Name = "Mesaj")]
    public string Body { get; set; } = string.Empty;
}

public class SendBulkEmailViewModel
{
    [Required(ErrorMessage = "Konu zorunludur.")]
    [Display(Name = "Konu")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mesaj zorunludur.")]
    [Display(Name = "Mesaj")]
    public string Body { get; set; } = string.Empty;

    [Display(Name = "Toplam Ogrenci")]
    public int TotalStudents { get; set; }
}
