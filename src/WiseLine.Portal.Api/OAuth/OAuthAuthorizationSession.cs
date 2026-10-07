using System.Globalization;
using System.Security.Claims;
using OpenIddict.Abstractions;

namespace WiseLine.Portal.Api.OAuth;

internal static class OAuthAuthorizationSession
{
    internal const string StartedAtClaim = "wiseline:authorization_started_at";

    internal static void SetStartedAt(ClaimsIdentity identity, DateTimeOffset startedAt) =>
        identity.SetClaim(
            StartedAtClaim,
            startedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));

    internal static bool TryGetStartedAt(
        ClaimsPrincipal principal,
        out DateTimeOffset startedAt)
    {
        startedAt = default;
        if (!long.TryParse(
                principal.GetClaim(StartedAtClaim),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var unixSeconds))
        {
            return false;
        }

        try
        {
            startedAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    internal static bool IsExpired(
        DateTimeOffset startedAt,
        DateTimeOffset now,
        int absoluteLifetimeDays) =>
        now >= startedAt.AddDays(absoluteLifetimeDays);
}
