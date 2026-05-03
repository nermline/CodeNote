using System.ComponentModel.DataAnnotations;

namespace NoteInfrastructure.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Поле обов'язкове")]
    [Display(Name = "Email")]
    [EmailAddress(ErrorMessage = "Невірний формат email")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Поле обов'язкове")]
    [DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = null!;

    [Display(Name = "Запам'ятати?")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
