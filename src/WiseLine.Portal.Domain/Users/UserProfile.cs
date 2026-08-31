namespace WiseLine.Portal.Domain.Users;

public sealed class UserProfile
{
    private UserProfile()
    {
    }

    public UserProfile(Guid userId, string displayName, DateTimeOffset now)
    {
        UserId = userId;
        DisplayName = NormalizeDisplayName(displayName);
        TimeZone = "UTC";
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid UserId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string TimeZone { get; private set; } = "UTC";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string displayName, string timeZone, DateTimeOffset now)
    {
        DisplayName = NormalizeDisplayName(displayName);
        TimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone.Trim();
        UpdatedAt = now;
    }

    private static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A display name is required.", nameof(displayName));
        }

        return displayName.Trim();
    }
}
