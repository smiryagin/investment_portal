using System.ComponentModel.DataAnnotations;

namespace WiseLine.Portal.Api.Contracts;

public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(12), MaxLength(128)] string Password,
    [Required, MaxLength(120)] string DisplayName);

public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(128)] string Password,
    bool RememberMe = false);

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool HasGoogleLogin);

public sealed record RedeemPromotionRequest(
    [Required, MaxLength(64)] string Code);

public sealed record CreateMcpTokenRequest(
    [Required, MaxLength(100)] string DisplayName);

public sealed record CheckoutResponse(string RedirectUrl, string SessionId);
