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
                options.Configure(configuration =>
                    configuration.ClientAuthenticationMethods.Add(ClientAuthenticationMethods.None));
                options.RegisterScopes(Scopes.OfflineAccess, "investments.read", "investments.write");
                options.RegisterResources(settings.Resource);
                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(settings.AccessTokenMinutes));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(settings.RefreshTokenDays));
                options.DisableAccessTokenEncryption();

                AddServerCertificates(options, settings, environment);

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableStatusCodePagesIntegration();
            });

        return services;
    }

    private static void AddServerCertificates(
        OpenIddictServerBuilder options,
        OAuthServerOptions settings,
        IHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(settings.SigningCertificateThumbprint) &&
            !string.IsNullOrWhiteSpace(settings.EncryptionCertificateThumbprint))
        {
            options.AddSigningCertificate(LoadCertificate(
                settings.SigningCertificateThumbprint,
                "signing"));
            options.AddEncryptionCertificate(LoadCertificate(
                settings.EncryptionCertificateThumbprint,
                "encryption"));
            return;
        }

        if (environment.IsDevelopment() && settings.UseDevelopmentSigningCertificate)
        {
            options.AddDevelopmentSigningCertificate();
            options.AddDevelopmentEncryptionCertificate();
            return;
        }

        throw new InvalidOperationException(
            "OAuth signing and encryption certificate thumbprints are required outside local development.");
    }

    private static X509Certificate2 LoadCertificate(string thumbprint, string purpose)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);
        var matches = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal),
            // Self-signed OAuth certificates do not need to chain to a trusted
            // CA. Their identity and validity are checked explicitly instead.
            validOnly: false);
        var now = DateTime.UtcNow;
        return matches
            .OfType<X509Certificate2>()
            .SingleOrDefault(x =>
                x.HasPrivateKey &&
                x.NotBefore.ToUniversalTime() <= now &&
                x.NotAfter.ToUniversalTime() > now)
            ?? throw new InvalidOperationException(
                $"The configured OAuth {purpose} certificate was not found, is outside its validity period, or has no private key.");
    }
}
