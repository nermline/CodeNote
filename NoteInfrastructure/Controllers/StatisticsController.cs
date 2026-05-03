using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NoteInfrastructure.Controllers;

    [Authorize(Roles = "admin, user")]
public class StatisticsController : Controller
{
    public IActionResult Index()
    {
        ViewData["Title"] = "Статистика";
        return View();
    }
}
