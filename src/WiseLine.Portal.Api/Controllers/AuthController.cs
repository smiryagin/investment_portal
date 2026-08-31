using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WiseLine.Portal.Api.Contracts;
using WiseLine.Portal.Api.Security;
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
    IConfiguration configuration) : ControllerBase
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
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await signInManager.SignInAsync(user, isPersistent: false);

        return Ok(new CurrentUserResponse(user.Id, email, user.DisplayName, false));
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
            logins.Any(x => x.LoginProvider == GoogleDefaults.AuthenticationScheme)));
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
            logins.Any(x => x.LoginProvider == GoogleDefaults.AuthenticationScheme)));
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

            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
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

                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    return LocalRedirect("/login?externalError=account-creation-failed");
                }

                dbContext.UserProfiles.Add(new UserProfile(user.Id, displayName, now));
                dbContext.Subscriptions.Add(Subscription.CreatePending(user.Id, now));
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            var addLoginResult = await userManager.AddLoginAsync(user, info);
            if (!addLoginResult.Succeeded)
            {
                return LocalRedirect("/login?externalError=login-link-failed");
            }

            await signInManager.SignInAsync(user, isPersistent: false);
        }

        return LocalRedirect(safeReturnUrl!);
    }

    private static Dictionary<string, string[]> ToValidationErrors(IdentityResult result) =>
        result.Errors
            .GroupBy(error => string.IsNullOrWhiteSpace(error.Code) ? "identity" : error.Code)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
}
