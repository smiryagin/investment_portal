using System.Security.Claims;

namespace WiseLine.Portal.Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("The authenticated user identifier is invalid.");
    }
}
