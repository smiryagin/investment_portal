using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using WiseLine.Portal.Api.Contracts;
using WiseLine.Portal.Api.Security;
using WiseLine.Portal.Application.Email;
using WiseLine.Portal.Domain.Subscriptions;
using WiseLine.Portal.Domain.Users;
using WiseLine.Portal.Infrastructure.Identity;
using WiseLine.Portal.Infrastructure.Persistence;

namespace WiseLine.Portal.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<PortalUser> userManager,
    SignInManager<PortalUser> signInManager,
    PortalDbContext dbContext,
    TimeProvider timeProvider,
    IConfiguration configuration,
    ITransactionalEmailOutbox emailOutbox) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<CurrentUserResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var now = timeProvider.GetUtcNow();
        var user = new PortalUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = now
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(ToValidationErrors(result)));
        }

        dbContext.UserProfiles.Add(new UserProfile(user.Id, user.DisplayName, now));
        dbContext.Subscriptions.Add(Subscription.CreatePending(user.Id, now));
        var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        emailOutbox.QueueEmailConfirmation(
            user.Id,
            email,
            user.DisplayName,
            BuildPublicUrl(
                "/api/auth/email-confirmation/confirm",
                new Dictionary<string, string?>
                {
                    ["userId"] = user.Id.ToString(),
                    ["token"] = EncodeToken(confirmationToken)
                }),
            now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await signInManager.SignInAsync(user, isPersistent: false);

        return Ok(new CurrentUserResponse(user.Id, email, user.DisplayName, false, user.EmailConfirmed));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<CurrentUserResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var result = await signInManager.PasswordSignInAsync(
            email,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "Account temporarily locked",
                detail: "Wait 15 minutes before trying again.");
        }

        if (result.IsNotAllowed)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Email confirmation required",
                detail: "Confirm your email address before logging in.");
        }

        if (!result.Succeeded)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Login failed",
                Detail = "Email or password is incorrect."
            });
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Unauthorized();
        }

        user.LastLoginAt = timeProvider.GetUtcNow();
        await userManager.UpdateAsync(user);
        var logins = await userManager.GetLoginsAsync(user);

        return Ok(new CurrentUserResponse(
            user.Id,
            user.Email ?? email,
            user.DisplayName,
            logins.Any(x => x.LoginProvider == GoogleDefaults.AuthenticationScheme),
            user.EmailConfirmed));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me()
    {
        var user = await userManager.FindByIdAsync(User.GetRequiredUserId().ToString());
        if (user is null)
        {
            return Unauthorized();
        }

        var logins = await userManager.GetLoginsAsync(user);
        return Ok(new CurrentUserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            logins.Any(x => x.LoginProvider == GoogleDefaults.AuthenticationScheme),
            user.EmailConfirmed));
    }

    [Authorize]
    [EnableRateLimiting("sensitive")]
    [HttpPost("email-confirmation/resend")]
    public async Task<IActionResult> ResendEmailConfirmation(CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(User.GetRequiredUserId().ToString());
        if (user is null)
        {
            return Unauthorized();
        }

        if (user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email))
        {
            return Accepted();
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        emailOutbox.QueueEmailConfirmation(
            user.Id,
            user.Email,
            user.DisplayName,
            BuildPublicUrl(
                "/api/auth/email-confirmation/confirm",
                new Dictionary<string, string?>
                {
                    ["userId"] = user.Id.ToString(),
                    ["token"] = EncodeToken(token)
                }),
            timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return Accepted();
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpGet("email-confirmation/confirm")]
    public async Task<IActionResult> ConfirmEmail(
        [FromQuery] Guid userId,
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !TryDecodeToken(token, out var decodedToken))
        {
            return LocalRedirect("/login?emailConfirmation=invalid");
        }

        if (!user.EmailConfirmed)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var result = await userManager.ConfirmEmailAsync(user, decodedToken);
            if (!result.Succeeded)
            {
                return LocalRedirect("/login?emailConfirmation=invalid");
            }

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                emailOutbox.QueueWelcome(
                    user.Id,
                    user.Email,
                    user.DisplayName,
                    timeProvider.GetUtcNow());
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        return LocalRedirect("/account?emailConfirmed=true");
    }

    [AllowAnonymous]
    [EnableRateLimiting("sensitive")]
    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null && !string.IsNullOrWhiteSpace(user.Email))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            emailOutbox.QueuePasswordReset(
                user.Id,
                user.Email,
                user.DisplayName,
                BuildPublicUrl(
                    "/reset-password",
                    new Dictionary<string, string?>
                    {
                        ["email"] = user.Email,
                        ["token"] = EncodeToken(token)
                    }),
                timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Accepted();
    }

    [AllowAnonymous]
    [EnableRateLimiting("sensitive")]
    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null || !TryDecodeToken(request.Token, out var decodedToken))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["token"] = ["The password reset link is invalid or has expired."]
            }));
        }

        var result = await userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);
        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(ToValidationErrors(result)));
        }

        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("google")]
    public IActionResult Google([FromQuery] string? returnUrl = "/dashboard")
    {
        if (string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"]))
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Google login is not configured");
        }

        var safeReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "/dashboard";
        var callbackUrl = Url.ActionLink(nameof(GoogleCallback), values: new { returnUrl = safeReturnUrl })!;
        var properties = signInManager.ConfigureExternalAuthenticationProperties(
            GoogleDefaults.AuthenticationScheme,
            callbackUrl);
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string? returnUrl = "/dashboard",
        [FromQuery] string? remoteError = null,
        CancellationToken cancellationToken = default)
    {
        var safeReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "/dashboard";
        if (!string.IsNullOrWhiteSpace(remoteError))
        {
            return LocalRedirect($"/login?externalError={Uri.EscapeDataString(remoteError)}");
        }

        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            return LocalRedirect("/login?externalError=missing-login-information");
        }

        var externalResult = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: false);

        if (!externalResult.Succeeded)
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                return LocalRedirect("/login?externalError=email-is-required");
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var user = await userManager.FindByEmailAsync(email);
            var isNewUser = user is null;
            if (isNewUser)
            {
                var now = timeProvider.GetUtcNow();
                var displayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email.Split('@')[0];
                user = new PortalUser
                {
                    Id = Guid.NewGuid(),
                    UserName = email.ToLowerInvariant(),
                    Email = email.ToLowerInvariant(),
                    EmailConfirmed = true,
                    DisplayName = displayName,
                    CreatedAt = now
                };

                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    return LocalRedirect("/login?externalError=account-creation-failed");
                }

                dbContext.UserProfiles.Add(new UserProfile(user.Id, displayName, now));
                dbContext.Subscriptions.Add(Subscription.CreatePending(user.Id, now));
                emailOutbox.QueueWelcome(user.Id, user.Email, displayName, now);
            }
            else if (!user!.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    return LocalRedirect("/login?externalError=account-update-failed");
                }
            }

            var addLoginResult = await userManager.AddLoginAsync(user!, info);
            if (!addLoginResult.Succeeded)
            {
                return LocalRedirect("/login?externalError=login-link-failed");
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await signInManager.SignInAsync(user!, isPersistent: false);
        }

        return LocalRedirect(safeReturnUrl!);
    }

    private static Dictionary<string, string[]> ToValidationErrors(IdentityResult result) =>
        result.Errors
            .GroupBy(error => string.IsNullOrWhiteSpace(error.Code) ? "identity" : error.Code)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());

    private string BuildPublicUrl(string path, IReadOnlyDictionary<string, string?>? query = null)
    {
        var baseUrl = configuration["Email:PublicBaseUrl"] ?? "https://wiselinetrade.com";
        var uri = new Uri(new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/"), path.TrimStart('/'));
        return query is null ? uri.ToString() : QueryHelpers.AddQueryString(uri.ToString(), query);
    }

    private static string EncodeToken(string token) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static bool TryDecodeToken(string token, out string decodedToken)
    {
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            return true;
        }
        catch (FormatException)
        {
            decodedToken = string.Empty;
            return false;
        }
    }
}
