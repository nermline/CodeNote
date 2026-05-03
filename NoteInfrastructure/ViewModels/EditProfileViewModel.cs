using System.ComponentModel.DataAnnotations;

namespace NoteInfrastructure.ViewModels;

public class EditProfileViewModel
{
    [Required(ErrorMessage = "Email обов'язковий")]
    [EmailAddress(ErrorMessage = "Невірний формат email")]
    [Display(Name = "Email")]
    public string Email { get; set; } = null!;

    [Display(Name = "Рік народження")]
    [Range(1900, 2100, ErrorMessage = "Вкажіть реальний рік народження")]
    public int? Year { get; set; }
}
