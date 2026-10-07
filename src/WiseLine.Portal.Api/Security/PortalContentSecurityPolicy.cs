using WiseLine.Portal.Infrastructure.OAuth;

namespace WiseLine.Portal.Api.Security;

internal static class PortalContentSecurityPolicy
{
    public static string Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var formActionSources = new List<string>
        {
            "'self'",
            "https://accounts.google.com"
        };
        var clients = configuration
            .GetSection($"{OAuthServerOptions.SectionName}:Clients")
            .Get<List<OAuthClientOptions>>() ?? [];
        foreach (var redirectUri in clients.SelectMany(client => client.RedirectUris))
        {
            if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
            {
                continue;
            }

            var source = GetFormActionSource(uri);
            if (source is not null && !formActionSources.Contains(source, StringComparer.Ordinal))
            {
                formActionSources.Add(source);
            }
        }

        return
            "default-src 'self'; script-src 'self' https://static.cloudflareinsights.com; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data:; font-src 'self'; connect-src 'self'; object-src 'none'; " +
            "base-uri 'self'; frame-ancestors 'none'; " +
            $"form-action {string.Join(' ', formActionSources)}";
    }

    private static string? GetFormActionSource(Uri uri)
    {
        if (uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp)
        {
            var host = uri.HostNameType == UriHostNameType.IPv6
                ? $"[{uri.Host.Trim('[', ']')}]"
                : uri.Host;
            return $"{uri.Scheme}://{host}:*";
        }

        return uri.Scheme == Uri.UriSchemeHttps
            ? uri.GetLeftPart(UriPartial.Authority)
            : null;
    }
}
