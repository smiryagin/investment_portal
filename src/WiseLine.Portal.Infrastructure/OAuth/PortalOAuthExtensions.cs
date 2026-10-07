using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WiseLine.Portal.Infrastructure.OAuth;

public static class PortalOAuthExtensions
{
    public static IServiceCollection AddPortalOAuthServer(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var settings = configuration
            .GetSection(OAuthServerOptions.SectionName)
            .Get<OAuthServerOptions>() ?? new OAuthServerOptions();
        if (!settings.Enabled)
        {
            return services;
        }

        services.AddOpenIddict()
            .AddServer(options =>
            {
                options.SetIssuer(new Uri(settings.Issuer, UriKind.Absolute));
                options.SetAuthorizationEndpointUris("connect/authorize");
                options.SetTokenEndpointUris("connect/token");
                options.SetRevocationEndpointUris("connect/revoke");

                options.AllowAuthorizationCodeFlow();
                options.AllowRefreshTokenFlow();
                options.RequireProofKeyForCodeExchange();
                options.RegisterScopes(Scopes.OfflineAccess, "investments.read", "investments.write");
                options.RegisterResources(settings.Resource);
                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(settings.AccessTokenMinutes));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(settings.RefreshTokenDays));
                options.DisableAccessTokenEncryption();

                AddSigningCertificate(options, settings, environment);

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableStatusCodePagesIntegration();
            });

        return services;
    }

    private static void AddSigningCertificate(
        OpenIddictServerBuilder options,
        OAuthServerOptions settings,
        IHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(settings.SigningCertificateThumbprint))
        {
            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var matches = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                settings.SigningCertificateThumbprint.Replace(" ", string.Empty, StringComparison.Ordinal),
                // A self-signed OAuth token-signing certificate does not need to
                // chain to a trusted CA. Validate its dates explicitly below.
                validOnly: false);
            var now = DateTime.UtcNow;
            var certificate = matches
                .OfType<X509Certificate2>()
                .SingleOrDefault(x =>
                    x.HasPrivateKey &&
                    x.NotBefore.ToUniversalTime() <= now &&
                    x.NotAfter.ToUniversalTime() > now)
                ?? throw new InvalidOperationException(
                    "The configured OAuth signing certificate was not found, is outside its validity period, or has no private key.");
            options.AddSigningCertificate(certificate);
            return;
        }

        if (environment.IsDevelopment() && settings.UseDevelopmentSigningCertificate)
        {
            options.AddDevelopmentSigningCertificate();
            return;
        }

        throw new InvalidOperationException(
            "OAuth:SigningCertificateThumbprint is required outside local development.");
    }
}
