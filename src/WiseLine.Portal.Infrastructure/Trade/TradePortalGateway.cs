using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using WiseLine.Portal.Application.Trade;
using WiseLine.Portal.Domain.Subscriptions;
using WiseLine.Portal.Domain.Users;
using WiseLine.Portal.Infrastructure.Persistence;

namespace WiseLine.Portal.Infrastructure.Trade;

public sealed class TradePortalGateway(
    IConfiguration configuration,
    IOptions<TradeDatabaseOptions> options,
    PortalDbContext portalDbContext,
    TimeProvider timeProvider) : ITradePortalGateway
{
    private readonly TradeDatabaseOptions _options = options.Value;
    private readonly string? _connectionString = configuration.GetConnectionString("TradeDatabase");

    public async Task SynchronizeEntitlementAsync(
        Guid portalUserId,
        CancellationToken cancellationToken = default)
    {
        _ = await SynchronizeAndResolveTradeUserIdAsync(portalUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<PortfolioSummary>> GetPortfoliosAsync(
        Guid portalUserId,
        CancellationToken cancellationToken = default)
    {
        var tradeUserId = await SynchronizeAndResolveTradeUserIdAsync(portalUserId, cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, _options.GetPortfoliosProcedure, tradeUserId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<PortfolioSummary>();

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new PortfolioSummary(
                reader.GetGuid(reader.GetOrdinal("PortfolioId")),
                reader.GetString(reader.GetOrdinal("Name")),
                GetNullableString(reader, "StrategyName"),
                reader.GetDecimal(reader.GetOrdinal("MarketValue")),
                reader.GetDecimal(reader.GetOrdinal("DayChange")),
                reader.GetDecimal(reader.GetOrdinal("DayChangePercent")),
                reader.GetInt32(reader.GetOrdinal("PositionCount")),
                GetNullableDateTimeOffset(reader, "UpdatedAt")));
        }

        return result;
    }

    public async Task<PortfolioDetails?> GetPortfolioAsync(
        Guid portalUserId,
        Guid portfolioId,
        CancellationToken cancellationToken = default)
    {
        var tradeUserId = await SynchronizeAndResolveTradeUserIdAsync(portalUserId, cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, _options.GetPortfolioProcedure, tradeUserId);
        command.Parameters.Add(new SqlParameter("@PortfolioId", SqlDbType.UniqueIdentifier) { Value = portfolioId });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var id = reader.GetGuid(reader.GetOrdinal("PortfolioId"));
        var name = reader.GetString(reader.GetOrdinal("Name"));
        var description = GetNullableString(reader, "Description");
        var strategyName = GetNullableString(reader, "StrategyName");
        var marketValue = reader.GetDecimal(reader.GetOrdinal("MarketValue"));
        var totalCost = reader.GetDecimal(reader.GetOrdinal("TotalCost"));
        var unrealizedGain = reader.GetDecimal(reader.GetOrdinal("UnrealizedGain"));
        var unrealizedGainPercent = reader.GetDecimal(reader.GetOrdinal("UnrealizedGainPercent"));
        var positions = new List<PortfolioPosition>();

        if (await reader.NextResultAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                positions.Add(new PortfolioPosition(
                    reader.GetString(reader.GetOrdinal("PositionId")),
                    reader.GetString(reader.GetOrdinal("Symbol")),
                    GetNullableString(reader, "Description"),
                    reader.GetDecimal(reader.GetOrdinal("Quantity")),
                    GetNullableDecimal(reader, "AveragePrice"),
                    GetNullableDecimal(reader, "CurrentPrice"),
                    reader.GetDecimal(reader.GetOrdinal("MarketValue")),
                    reader.GetDecimal(reader.GetOrdinal("UnrealizedGain")),
                    reader.GetDecimal(reader.GetOrdinal("UnrealizedGainPercent"))));
            }
        }

        return new PortfolioDetails(
            id,
            name,
            description,
            strategyName,
            marketValue,
            totalCost,
            unrealizedGain,
            unrealizedGainPercent,
            positions);
    }

    public async Task<IReadOnlyList<McpTokenSummary>> GetMcpTokensAsync(
        Guid portalUserId,
        CancellationToken cancellationToken = default)
    {
        var tradeUserId = await SynchronizeAndResolveTradeUserIdAsync(portalUserId, cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, _options.GetMcpTokensProcedure, tradeUserId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<McpTokenSummary>();

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new McpTokenSummary(
                reader.GetGuid(reader.GetOrdinal("TokenId")),
                reader.GetString(reader.GetOrdinal("DisplayName")),
                reader.GetString(reader.GetOrdinal("TokenPrefix")),
                reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("CreatedAt")),
                GetNullableDateTimeOffset(reader, "LastUsedAt"),
                GetNullableDateTimeOffset(reader, "ExpiresAt"),
                reader.GetBoolean(reader.GetOrdinal("IsRevoked"))));
        }

        return result;
    }

    public async Task<McpTokenCreated> CreateMcpTokenAsync(
        Guid portalUserId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A token name is required.", nameof(displayName));
        }

        var tradeUserId = await SynchronizeAndResolveTradeUserIdAsync(portalUserId, cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, _options.CreateMcpTokenProcedure, tradeUserId);
        command.Parameters.Add(new SqlParameter("@DisplayName", SqlDbType.NVarChar, 100) { Value = displayName.Trim() });
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("The Trade database did not return the created MCP token.");
        }

        return new McpTokenCreated(
            reader.GetGuid(reader.GetOrdinal("TokenId")),
            reader.GetString(reader.GetOrdinal("DisplayName")),
            reader.GetString(reader.GetOrdinal("Token")),
            reader.GetString(reader.GetOrdinal("TokenPrefix")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("CreatedAt")),
            GetNullableDateTimeOffset(reader, "ExpiresAt"));
    }

    public async Task RevokeMcpTokenAsync(
        Guid portalUserId,
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        var tradeUserId = await SynchronizeAndResolveTradeUserIdAsync(portalUserId, cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, _options.RevokeMcpTokenProcedure, tradeUserId);
        command.Parameters.Add(new SqlParameter("@TokenId", SqlDbType.UniqueIdentifier) { Value = tokenId });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<Guid> SynchronizeAndResolveTradeUserIdAsync(
        Guid portalUserId,
        CancellationToken cancellationToken)
    {
        var identity = await portalDbContext.Users
            .Where(x => x.Id == portalUserId)
            .Select(x => new { x.DisplayName, x.Email })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new TradeIntegrationUnavailableException("The authenticated portal account no longer exists.");

        var subscription = await portalDbContext.Subscriptions
            .SingleOrDefaultAsync(x => x.UserId == portalUserId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var isEntitled = subscription?.IsEntitledAt(now) ?? false;
        var entitledThrough = subscription?.Status switch
        {
            SubscriptionStatus.Trialing => subscription.TrialEndsAt,
            SubscriptionStatus.Active => subscription.CurrentPeriodEndsAt,
            _ => null
        };

        var tradeUserId = await portalDbContext.InvestmentIdentityLinks
            .Where(x => x.PortalUserId == portalUserId)
            .Select(x => (Guid?)x.TradeUserId)
            .SingleOrDefaultAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        if (tradeUserId is null)
        {
            await using var ensureCommand = CreateCommand(connection, _options.EnsureUserProcedure);
            ensureCommand.Parameters.Add(new SqlParameter("@PortalUserId", SqlDbType.UniqueIdentifier) { Value = portalUserId });
            ensureCommand.Parameters.Add(new SqlParameter("@DisplayName", SqlDbType.NVarChar, 200) { Value = identity.DisplayName });
            ensureCommand.Parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 320)
            {
                Value = string.IsNullOrWhiteSpace(identity.Email) ? DBNull.Value : identity.Email
            });

            var result = await ensureCommand.ExecuteScalarAsync(cancellationToken);
            tradeUserId = result is Guid value && value != Guid.Empty
                ? value
                : throw new TradeIntegrationUnavailableException(
                    "The Trade database did not return an identity for this portal account.");

            var link = new InvestmentIdentityLink(portalUserId, tradeUserId.Value, now);
            portalDbContext.InvestmentIdentityLinks.Add(link);
            try
            {
                await portalDbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                portalDbContext.Entry(link).State = EntityState.Detached;
                var existingTradeUserId = await portalDbContext.InvestmentIdentityLinks
                    .Where(x => x.PortalUserId == portalUserId)
                    .Select(x => (Guid?)x.TradeUserId)
                    .SingleOrDefaultAsync(cancellationToken);
                if (existingTradeUserId is null || existingTradeUserId != tradeUserId)
                {
                    throw;
                }
            }
        }

        await using var entitlementCommand = CreateCommand(connection, _options.SetEntitlementProcedure, tradeUserId.Value);
        entitlementCommand.Parameters.Add(new SqlParameter("@PortalUserId", SqlDbType.UniqueIdentifier) { Value = portalUserId });
        entitlementCommand.Parameters.Add(new SqlParameter("@IsEntitled", SqlDbType.Bit) { Value = isEntitled });
        entitlementCommand.Parameters.Add(new SqlParameter("@EntitledThrough", SqlDbType.DateTimeOffset)
        {
            Value = entitledThrough is null ? DBNull.Value : entitledThrough.Value
        });
        await entitlementCommand.ExecuteNonQueryAsync(cancellationToken);

        return tradeUserId.Value;
    }

    private SqlConnection CreateConnection()
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new TradeIntegrationUnavailableException(
                "Investment data integration is not configured for this environment.");
        }

        return new SqlConnection(_connectionString);
    }

    private static SqlCommand CreateCommand(SqlConnection connection, string procedureName)
    {
        var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = procedureName;
        command.CommandTimeout = 30;
        return command;
    }

    private static SqlCommand CreateCommand(SqlConnection connection, string procedureName, Guid tradeUserId)
    {
        var command = CreateCommand(connection, procedureName);
        command.Parameters.Add(new SqlParameter("@TradeUserId", SqlDbType.UniqueIdentifier) { Value = tradeUserId });
        return command;
    }

    private static string? GetNullableString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static decimal? GetNullableDecimal(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }
}
