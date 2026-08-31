using Microsoft.AspNetCore.Identity;

namespace WiseLine.Portal.Infrastructure.Identity;

public sealed class PortalUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}
