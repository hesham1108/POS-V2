using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace StoresPOS.PL.Controllers;

public static class ControllerExtensions
{
    public static int GetRequiredUserId(this Controller controller)
    {
        var raw = controller.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(raw, out var userId) || userId <= 0)
        {
            throw new InvalidOperationException("Authenticated user id is missing from the token.");
        }

        return userId;
    }
}
