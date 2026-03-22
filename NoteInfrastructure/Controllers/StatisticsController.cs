using Microsoft.AspNetCore.Mvc;

namespace NoteInfrastructure.Controllers;

public class StatisticsController : Controller
{
    public IActionResult Index()
    {
        ViewData["Title"] = "Статистика";
        return View();
    }
}
