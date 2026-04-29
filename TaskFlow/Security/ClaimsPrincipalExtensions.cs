using System.Security.Claims;

namespace TaskFlow.Security;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId) ? userId : 0;
    }

    public static bool HasElevatedAccess(this ClaimsPrincipal principal)
    {
        return principal.IsInRole(AppRoles.Manager) || principal.IsInRole(AppRoles.Admin);
    }
}
