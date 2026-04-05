using System.Security.Claims;

namespace NetWorth.Services;

public class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
{
    public string GetRequiredUserId()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        var oid = user.FindFirstValue("oid");
        if (!string.IsNullOrWhiteSpace(oid))
        {
            return oid;
        }

        var sub = user.FindFirstValue("sub");
        if (!string.IsNullOrWhiteSpace(sub))
        {
            return sub;
        }

        throw new InvalidOperationException("User identifier claim not found. Expected 'oid' or 'sub'.");
    }
}

