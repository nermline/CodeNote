namespace NoteInfrastructure.ViewModels;

public class AdminUserViewModel
{
    public string Id           { get; set; } = null!;
    public string Email        { get; set; } = null!;
    public int?   Year         { get; set; }
    public bool   IsLockedOut  { get; set; }
    public bool   IsLastAdmin  { get; set; }
    public IList<string> Roles { get; set; } = [];
}

public class AdminResetPasswordViewModel
{
    public string UserId    { get; set; } = null!;
    public string UserEmail { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Новий пароль обов'язковий")]
    [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
    [System.ComponentModel.DataAnnotations.Display(Name = "Новий пароль")]
    [System.ComponentModel.DataAnnotations.MinLength(6, ErrorMessage = "Мінімум 6 символів")]
    [System.ComponentModel.DataAnnotations.RegularExpression(
        @"^(?=.*[0-9]).{6,}$",
        ErrorMessage = "Пароль має містити щонайменше одну цифру")]
    public string NewPassword { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Підтвердження обов'язкове")]
    [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
    [System.ComponentModel.DataAnnotations.Display(Name = "Підтвердження паролю")]
    [System.ComponentModel.DataAnnotations.Compare("NewPassword", ErrorMessage = "Паролі не співпадають")]
    public string ConfirmPassword { get; set; } = null!;
}
