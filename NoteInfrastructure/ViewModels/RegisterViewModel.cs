using System.ComponentModel.DataAnnotations;

namespace NoteInfrastructure.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Поле обов'язкове")]
    [Display(Name = "Email")]
    [EmailAddress(ErrorMessage = "Невірний формат email")]
    public string Email { get; set; } = null!;

    [Display(Name = "Рік народження")]
    [Range(1900, 2100, ErrorMessage = "Вкажіть реальний рік народження")]
    public int? Year { get; set; }

    [Required(ErrorMessage = "Поле обов'язкове")]
    [DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    [MinLength(6, ErrorMessage = "Пароль має містити щонайменше 6 символів")]
    [RegularExpression(
        @"^(?=.*[0-9]).{6,}$",
        ErrorMessage = "Пароль має містити щонайменше одну цифру")]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Поле обов'язкове")]
    [Compare("Password", ErrorMessage = "Паролі не співпадають")]
    [DataType(DataType.Password)]
    [Display(Name = "Підтвердження паролю")]
    public string PasswordConfirm { get; set; } = null!;
}
