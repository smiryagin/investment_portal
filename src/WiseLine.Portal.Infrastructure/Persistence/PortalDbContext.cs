using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WiseLine.Portal.Domain.Auditing;
using WiseLine.Portal.Domain.Payments;
using WiseLine.Portal.Domain.Subscriptions;
using WiseLine.Portal.Domain.Users;
using WiseLine.Portal.Infrastructure.Identity;

namespace WiseLine.Portal.Infrastructure.Persistence;

public sealed class PortalDbContext(DbContextOptions<PortalDbContext> options)
    : IdentityDbContext<PortalUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<PromotionCode> PromotionCodes => Set<PromotionCode>();

    public DbSet<PromotionRedemption> PromotionRedemptions => Set<PromotionRedemption>();

    public DbSet<InvestmentIdentityLink> InvestmentIdentityLinks => Set<InvestmentIdentityLink>();

    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentity(builder);
        ConfigureUsers(builder);
        ConfigureSubscriptions(builder);
        ConfigurePayments(builder);
        ConfigureAudit(builder);
    }

    private static void ConfigureIdentity(ModelBuilder builder)
    {
        builder.Entity<PortalUser>(entity =>
        {
            entity.ToTable("Users", "auth");
            entity.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.CreatedAt).HasPrecision(0);
            entity.Property(x => x.LastLoginAt).HasPrecision(0);
        });
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles", "auth");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles", "auth");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims", "auth");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins", "auth");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims", "auth");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens", "auth");
    }

    private static void ConfigureUsers(ModelBuilder builder)
    {
        builder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("UserProfiles", "portal");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.TimeZone).HasMaxLength(80).IsRequired();
            entity.Property(x => x.CreatedAt).HasPrecision(0);
            entity.Property(x => x.UpdatedAt).HasPrecision(0);
            entity.HasOne<PortalUser>().WithOne().HasForeignKey<UserProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InvestmentIdentityLink>(entity =>
        {
            entity.ToTable("InvestmentIdentityLinks", "integration");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.PortalUserId).IsUnique();
            entity.HasIndex(x => x.TradeUserId).IsUnique();
            entity.Property(x => x.CreatedAt).HasPrecision(0);
            entity.HasOne<PortalUser>().WithOne().HasForeignKey<InvestmentIdentityLink>(x => x.PortalUserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSubscriptions(ModelBuilder builder)
    {
        builder.Entity<Subscription>(entity =>
        {
            entity.ToTable("Subscriptions", "billing");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => new { x.Provider, x.ProviderSubscriptionId }).IsUnique().HasFilter("[ProviderSubscriptionId] IS NOT NULL");
            entity.Property(x => x.PlanKey).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Provider).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.ProviderCustomerId).HasMaxLength(160);
            entity.Property(x => x.ProviderSubscriptionId).HasMaxLength(160);
            entity.Property(x => x.TrialStartedAt).HasPrecision(0);
            entity.Property(x => x.TrialEndsAt).HasPrecision(0);
            entity.Property(x => x.CurrentPeriodEndsAt).HasPrecision(0);
            entity.Property(x => x.CanceledAt).HasPrecision(0);
            entity.Property(x => x.CreatedAt).HasPrecision(0);
            entity.Property(x => x.UpdatedAt).HasPrecision(0);
            entity.HasOne<PortalUser>().WithOne().HasForeignKey<Subscription>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PromotionCode>(entity =>
        {
            entity.ToTable("PromotionCodes", "billing");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.StartsAt).HasPrecision(0);
            entity.Property(x => x.ExpiresAt).HasPrecision(0);
            entity.Property(x => x.CreatedAt).HasPrecision(0);
        });

        builder.Entity<PromotionRedemption>(entity =>
        {
            entity.ToTable("PromotionRedemptions", "billing");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.PromotionCodeId, x.UserId }).IsUnique();
            entity.Property(x => x.RedeemedAt).HasPrecision(0);
            entity.HasOne<PromotionCode>().WithMany().HasForeignKey(x => x.PromotionCodeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PortalUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePayments(ModelBuilder builder)
    {
        builder.Entity<PaymentWebhookEvent>(entity =>
        {
            entity.ToTable("WebhookEvents", "billing");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Provider, x.ProviderEventId }).IsUnique();
            entity.Property(x => x.Provider).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ProviderEventId).HasMaxLength(160).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.PayloadSha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(x => x.ReceivedAt).HasPrecision(0);
            entity.Property(x => x.ProcessedAt).HasPrecision(0);
            entity.Property(x => x.ProcessingError).HasMaxLength(2000);
        });
    }

    private static void ConfigureAudit(ModelBuilder builder)
    {
        builder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("AuditEvents", "audit");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.OccurredAt });
            entity.HasIndex(x => x.OccurredAt);
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Outcome).HasMaxLength(40).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.OccurredAt).HasPrecision(0);
        });
    }
}
