using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoresPOS.BLL.Constants;
using StoresPOS.PL.Models;

namespace StoresPOS.PL.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        // Cashiers (no back-office access) always pass through the shift gate (journeys A/B/C).
        if (!User.HasPermission(AppPermissions.DashboardView))
        {
            return RedirectToAction("Gate", "Shifts");
        }

        return View();
    }

    [Authorize(Policy = AppPermissions.DashboardView)]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
