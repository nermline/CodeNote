using System.ComponentModel.DataAnnotations;

namespace NoteInfrastructure.ViewModels;

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Поточний пароль обов'язковий")]
    [DataType(DataType.Password)]
    [Display(Name = "Поточний пароль")]
    public string CurrentPassword { get; set; } = null!;

    [Required(ErrorMessage = "Новий пароль обов'язковий")]
    [DataType(DataType.Password)]
    [Display(Name = "Новий пароль")]
    [MinLength(6, ErrorMessage = "Пароль має містити щонайменше 6 символів")]
    [RegularExpression(
        @"^(?=.*[0-9]).{6,}$",
        ErrorMessage = "Пароль має містити щонайменше одну цифру")]
    public string NewPassword { get; set; } = null!;

    [Required(ErrorMessage = "Підтвердження паролю обов'язкове")]
    [DataType(DataType.Password)]
    [Display(Name = "Підтвердження нового паролю")]
    [Compare("NewPassword", ErrorMessage = "Паролі не співпадають")]
    public string ConfirmPassword { get; set; } = null!;
}
