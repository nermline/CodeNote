using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NoteInfrastructure.Controllers;

[Authorize(Roles = "admin, user")]
public abstract class BaseUserController : Controller
{

    protected string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Користувач не авторизований.");
}
