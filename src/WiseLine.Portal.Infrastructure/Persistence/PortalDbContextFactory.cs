using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WiseLine.Portal.Infrastructure.Persistence;

public sealed class PortalDbContextFactory : IDesignTimeDbContextFactory<PortalDbContext>
{
    public PortalDbContext CreateDbContext(string[] args)
    {
        const string fallbackConnection =
            "Server=(localdb)\\MSSQLLocalDB;Database=WiseLinePortal;Trusted_Connection=True;TrustServerCertificate=True";

        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__PortalDatabase") ?? fallbackConnection;

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseSqlServer(
                connectionString,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "deployment"))
            .Options;

        return new PortalDbContext(options);
    }
}
