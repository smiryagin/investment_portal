using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WiseLine.Portal.Application.Email;
using WiseLine.Portal.Application.Payments;
using WiseLine.Portal.Application.Subscriptions;
using WiseLine.Portal.Application.Trade;
using WiseLine.Portal.Infrastructure.Email;
using WiseLine.Portal.Infrastructure.Identity;
using WiseLine.Portal.Infrastructure.OAuth;
using WiseLine.Portal.Infrastructure.Payments;
using WiseLine.Portal.Infrastructure.Persistence;
using WiseLine.Portal.Infrastructure.Subscriptions;
using WiseLine.Portal.Infrastructure.Trade;

namespace WiseLine.Portal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPortalInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var portalConnection = configuration.GetConnectionString("PortalDatabase");
        if (string.IsNullOrWhiteSpace(portalConnection))
        {
            throw new InvalidOperationException("ConnectionStrings:PortalDatabase is required.");
        }

        services.AddDbContext<PortalDbContext>(options =>
            options.UseSqlServer(
                portalConnection,
                sql =>
                {
                    sql.EnableRetryOnFailure(3);
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", "deployment");
                })
                .UseOpenIddict());

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<PortalDbContext>();
            });

        services
            .AddIdentityCore<PortalUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddSignInManager()
            .AddEntityFrameworkStores<PortalDbContext>()
            .AddDefaultTokenProviders();

        services.AddSingleton(TimeProvider.System);
        services.AddHostedService<OAuthClientSeeder>();
        services.AddHttpClient("OAuthClientMetadata", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WiseLinePortal/1.0");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
        services.AddScoped<ITransactionalEmailOutbox, TransactionalEmailOutbox>();
        services.AddScoped<IEmailWebhookProcessor, ResendWebhookProcessor>();
        services.AddSingleton<IEmailWebhookSignatureVerifier, ResendWebhookSignatureVerifier>();
        services.AddHostedService<EmailOutboxWorker>();
        services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
        {
            client.BaseAddress = new Uri("https://api.resend.com/");
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WiseLinePortal/1.0");
        });
        services.AddScoped<ISubscriptionAccessService, SubscriptionAccessService>();
        services.AddScoped<ITradePortalGateway, TradePortalGateway>();
        services.AddHostedService<TradeEntitlementSyncWorker>();
        services.AddScoped<IPaymentCheckoutService, PaymentCheckoutService>();
        services.AddScoped<IPaymentWebhookProcessor, PaymentWebhookProcessor>();
        services.AddHttpClient("Stripe", client => client.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient("PayPal", client => client.Timeout = TimeSpan.FromSeconds(20));

        services
            .AddOptions<OAuthServerOptions>()
            .Bind(configuration.GetSection(OAuthServerOptions.SectionName))
            .Validate(
                value => !value.Enabled || IsAbsoluteHttpsUrl(value.Issuer),
                "OAuth:Issuer must be an absolute HTTPS URL when OAuth is enabled.")
            .Validate(
                value => !value.Enabled || IsAbsoluteHttpsUrl(value.Resource),
                "OAuth:Resource must be an absolute HTTPS URL when OAuth is enabled.")
            .Validate(
                value => value.AccessTokenMinutes is >= 5 and <= 60,
                "OAuth:AccessTokenMinutes must be between 5 and 60.")
            .Validate(
                value => value.RefreshTokenDays is >= 1 and <= 90,
                "OAuth:RefreshTokenDays must be between 1 and 90.")
            .Validate(
                value => !value.Enabled ||
                    value.UseDevelopmentSigningCertificate ||
                    (!string.IsNullOrWhiteSpace(value.SigningCertificateThumbprint) &&
                     !string.IsNullOrWhiteSpace(value.EncryptionCertificateThumbprint)),
                "OAuth signing and encryption certificate thumbprints are required when OAuth is enabled.")
            .Validate(
                value => !value.Enabled || value.Clients.All(IsValidOAuthClient),
                "Every OAuth client requires a client id, display name, and either an HTTPS web redirect or the native loopback redirect http://127.0.0.1/callback.")
            .ValidateOnStart();

        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(
                value => !value.Enabled || !string.IsNullOrWhiteSpace(value.ApiKey),
                "Email:ApiKey is required when transactional email is enabled.")
            .Validate(
                value => !value.Enabled || MailAddress.TryCreate(value.FromAddress, out _),
                "Email:FromAddress must be a valid mailbox when transactional email is enabled.")
            .Validate(
                value => !value.Enabled ||
                    (Uri.TryCreate(value.PublicBaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps),
                "Email:PublicBaseUrl must be an absolute HTTPS URL when transactional email is enabled.")
            .Validate(
                value => value.OutboxPollSeconds is >= 2 and <= 300,
                "Email:OutboxPollSeconds must be between 2 and 300 seconds.")
            .ValidateOnStart();

        services
            .AddOptions<TradeDatabaseOptions>()
            .Bind(configuration.GetSection(TradeDatabaseOptions.SectionName))
            .Validate(
                value => !value.Enabled || !string.IsNullOrWhiteSpace(configuration.GetConnectionString("TradeDatabase")),
                "ConnectionStrings:TradeDatabase is required when TradeDatabase:Enabled is true.")
            .Validate(
                value => value.EntitlementSyncPollSeconds is >= 5 and <= 300,
                "TradeDatabase:EntitlementSyncPollSeconds must be between 5 and 300 seconds.")
            .ValidateOnStart();

        services
            .AddOptions<PaymentOptions>()
            .Bind(configuration.GetSection(PaymentOptions.SectionName))
            .Validate(
                value => Uri.TryCreate(value.PublicBaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                "Payments:PublicBaseUrl must be an absolute HTTPS URL.")
            .Validate(
                value => !value.Stripe.Enabled ||
                    (!string.IsNullOrWhiteSpace(value.Stripe.SecretKey) &&
                     !string.IsNullOrWhiteSpace(value.Stripe.PriceId) &&
                     !string.IsNullOrWhiteSpace(value.Stripe.WebhookSecret)),
                "Stripe SecretKey, PriceId, and WebhookSecret are required when Stripe is enabled.")
            .Validate(
                value => !value.PayPal.Enabled ||
                    (!string.IsNullOrWhiteSpace(value.PayPal.ClientId) &&
                     !string.IsNullOrWhiteSpace(value.PayPal.ClientSecret) &&
                     !string.IsNullOrWhiteSpace(value.PayPal.PlanId) &&
                     !string.IsNullOrWhiteSpace(value.PayPal.WebhookId)),
                "PayPal ClientId, ClientSecret, PlanId, and WebhookId are required when PayPal is enabled.")
            .ValidateOnStart();

        return services;
    }

    private static bool IsAbsoluteHttpsUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static bool IsValidOAuthRedirectUri(OAuthClientOptions client, string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (client.ApplicationType == "web")
        {
            return uri.Scheme == Uri.UriSchemeHttps;
        }

        return client.ApplicationType == "native" &&
            uri.Scheme == Uri.UriSchemeHttp &&
            uri.Host == "127.0.0.1" &&
            uri.IsDefaultPort &&
            uri.AbsolutePath == "/callback" &&
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment);
    }

    private static bool IsValidOAuthClient(OAuthClientOptions client) =>
        !string.IsNullOrWhiteSpace(client.ClientId) &&
        !string.IsNullOrWhiteSpace(client.DisplayName) &&
        client.ApplicationType is "web" or "native" &&
        client.RedirectUris.Count > 0 &&
        client.RedirectUris.All(value => IsValidOAuthRedirectUri(client, value)) &&
        client.TokenEndpointAuthenticationMethod is "none" or "private_key_jwt" &&
        (client.ApplicationType != "native" ||
            client.TokenEndpointAuthenticationMethod == "none") &&
        (client.TokenEndpointAuthenticationMethod != "private_key_jwt" ||
            IsAbsoluteHttpsUrl(client.JsonWebKeySetUri));
}
