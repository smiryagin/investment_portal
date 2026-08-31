using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WiseLine.Portal.Infrastructure;
using WiseLine.Portal.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var externalConfigFile = Environment.GetEnvironmentVariable("WISELINE_PORTAL_CONFIG_FILE");
if (!string.IsNullOrWhiteSpace(externalConfigFile))
{
    var fullConfigPath = Path.GetFullPath(externalConfigFile);
    builder.Configuration.AddJsonFile(fullConfigPath, optional: false, reloadOnChange: true);
}

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddPortalInfrastructure(builder.Configuration);

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services
        .AddAuthentication()
        .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
            options.SignInScheme = IdentityConstants.ExternalScheme;
        });
}

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-WiseLinePortal.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Path = "/";
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context => WriteApiStatusOrRedirect(context, StatusCodes.Status401Unauthorized);
    options.Events.OnRedirectToAccessDenied = context => WriteApiStatusOrRedirect(context, StatusCodes.Status403Forbidden);
});

builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddControllers(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<PortalDbContext>("portal-database", tags: ["ready"]);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("general", context =>
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var key = userId ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString();
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Detail = "Please wait before trying again."
            },
            cancellationToken);
    };
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; font-src 'self'; connect-src 'self'; object-src 'none'; " +
        "base-uri 'self'; frame-ancestors 'none'; form-action 'self' https://accounts.google.com";
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).AllowAnonymous();

app.MapControllers().RequireRateLimiting("general");

var angularIndex = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");
if (File.Exists(angularIndex))
{
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await context.Response.SendFileAsync(angularIndex);
    });
}

app.Run();

static Task WriteApiStatusOrRedirect(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }

    context.Response.Redirect(context.RedirectUri);
    return Task.CompletedTask;
}

public partial class Program;
