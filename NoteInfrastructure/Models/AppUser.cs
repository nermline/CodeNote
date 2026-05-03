using Microsoft.AspNetCore.Identity;

namespace NoteInfrastructure.Models;

public class AppUser : IdentityUser
{
    /// <summary>
    /// Рік реєстрації або будь-яке додаткове поле.
    /// Можна розширити за потреби.
    /// </summary>
    public int? Year { get; set; }
}
