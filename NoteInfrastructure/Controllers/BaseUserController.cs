using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NoteInfrastructure.Controllers;

/// <summary>
/// Базовий контролер, що надає зручний доступ до ідентифікатора поточного користувача.
/// </summary>
[Authorize(Roles = "admin, user")]
public abstract class BaseUserController : Controller
{
    /// <summary>
    /// Повертає Id поточного авторизованого користувача (AspNetUsers.Id).
    /// </summary>
    protected string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Користувач не авторизований.");
}
