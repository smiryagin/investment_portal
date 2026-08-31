using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WiseLine.Portal.Application.Payments;
using WiseLine.Portal.Application.Subscriptions;
using WiseLine.Portal.Application.Trade;
using WiseLine.Portal.Infrastructure.Identity;
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
                }));

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
        services.AddScoped<ISubscriptionAccessService, SubscriptionAccessService>();
        services.AddScoped<ITradePortalGateway, TradePortalGateway>();
        services.AddScoped<IPaymentCheckoutService, PaymentCheckoutService>();
        services.AddScoped<IPaymentWebhookProcessor, PaymentWebhookProcessor>();
        services.AddHttpClient("Stripe", client => client.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient("PayPal", client => client.Timeout = TimeSpan.FromSeconds(20));

        services
            .AddOptions<TradeDatabaseOptions>()
            .Bind(configuration.GetSection(TradeDatabaseOptions.SectionName))
            .Validate(
                value => !value.Enabled || !string.IsNullOrWhiteSpace(configuration.GetConnectionString("TradeDatabase")),
                "ConnectionStrings:TradeDatabase is required when TradeDatabase:Enabled is true.")
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
}
