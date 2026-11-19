using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoresPOS.BLL.Constants;
using StoresPOS.BLL.Interfaces;
using StoresPOS.PL.Models;

namespace StoresPOS.PL.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IDashboardService _dashboard;

    public HomeController(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Cashiers (no back-office access) always pass through the shift gate (journeys A/B/C).
        if (!User.HasPermission(AppPermissions.DashboardView))
        {
            return RedirectToAction("Gate", "Shifts");
        }

        return View(await _dashboard.GetAsync(cancellationToken));
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
