using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WiseLine.Portal.Infrastructure.OAuth;

public sealed class OAuthClientSeeder(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<OAuthServerOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<OAuthClientSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var applicationManager = scope.ServiceProvider
            .GetRequiredService<IOpenIddictApplicationManager>();

        foreach (var configuredClient in options.Value.Clients)
        {
            var existing = await applicationManager.FindByClientIdAsync(
                configuredClient.ClientId,
                cancellationToken);
            OpenIddictApplicationDescriptor descriptor;
            try
            {
                descriptor = await CreateDescriptorAsync(configuredClient, cancellationToken);
            }
            catch (HttpRequestException exception) when (existing is not null)
            {
                logger.LogWarning(
                    exception,
                    "OAuth client {ClientId} metadata could not be refreshed; the existing registration will be retained.",
                    configuredClient.ClientId);
                continue;
            }
            catch (TaskCanceledException exception) when (
                existing is not null && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    exception,
                    "OAuth client {ClientId} metadata refresh timed out; the existing registration will be retained.",
                    configuredClient.ClientId);
                continue;
            }

            if (existing is null)
            {
                await applicationManager.CreateAsync(descriptor, cancellationToken);
                logger.LogInformation(
                    "OAuth client registration {ClientId} was created.",
                    configuredClient.ClientId);
            }
            else
            {
                await applicationManager.UpdateAsync(existing, descriptor, cancellationToken);
                logger.LogInformation(
                    "OAuth client registration {ClientId} was refreshed.",
                    configuredClient.ClientId);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<OpenIddictApplicationDescriptor> CreateDescriptorAsync(
        OAuthClientOptions client,
        CancellationToken cancellationToken)
    {
        var usesPrivateKeyJwt = string.Equals(
            client.TokenEndpointAuthenticationMethod,
            ClientAuthenticationMethods.PrivateKeyJwt,
            StringComparison.Ordinal);
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            ClientType = usesPrivateKeyJwt ? ClientTypes.Confidential : ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            DisplayName = client.DisplayName
        };

        if (usesPrivateKeyJwt)
        {
            using var response = await httpClientFactory
                .CreateClient("OAuthClientMetadata")
                .GetAsync(client.JsonWebKeySetUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            const int maximumJwksBytes = 1_048_576;
            if (response.Content.Headers.ContentLength > maximumJwksBytes)
            {
                throw new InvalidOperationException("The OAuth client JWKS document is too large.");
            }

            var document = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (document.Length > maximumJwksBytes)
            {
                throw new InvalidOperationException("The OAuth client JWKS document is too large.");
            }

            var json = System.Text.Encoding.UTF8.GetString(document);
            descriptor.JsonWebKeySet = new JsonWebKeySet(json);
        }

        foreach (var redirectUri in client.RedirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(redirectUri, UriKind.Absolute));
        }

        descriptor.Permissions.UnionWith(
        [
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.Endpoints.Revocation,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
            Permissions.Prefixes.Scope + "investments.read",
            Permissions.Prefixes.Scope + "investments.write"
        ]);
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);

        return descriptor;
    }
}
