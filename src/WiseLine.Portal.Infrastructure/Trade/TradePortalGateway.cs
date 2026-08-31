using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using WiseLine.Portal.Application.Trade;

namespace WiseLine.Portal.Infrastructure.Trade;

public sealed class TradePortalGateway(
    IConfiguration configuration,
    IOptions<TradeDatabaseOptions> options) : ITradePortalGateway
{
    private readonly TradeDatabaseOptions _options = options.Value;
    private readonly string? _connectionString = configuration.GetConnectionString("TradeDatabase");

    public async Task<IReadOnlyList<PortfolioSummary>> GetPortfoliosAsync(
        Guid portalUserId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, _options.GetPortfoliosProcedure, portalUserId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<PortfolioSummary>();

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new PortfolioSummary(
                reader.GetInt64(reader.GetOrdinal("PortfolioId")),
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
        long portfolioId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, _options.GetPortfolioProcedure, portalUserId);
        command.Parameters.Add(new SqlParameter("@PortfolioId", SqlDbType.BigInt) { Value = portfolioId });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var id = reader.GetInt64(reader.GetOrdinal("PortfolioId"));
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
                    reader.GetInt64(reader.GetOrdinal("PositionId")),
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
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, _options.GetMcpTokensProcedure, portalUserId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<McpTokenSummary>();

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new McpTokenSummary(
                reader.GetInt64(reader.GetOrdinal("TokenId")),
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

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, _options.CreateMcpTokenProcedure, portalUserId);
        command.Parameters.Add(new SqlParameter("@DisplayName", SqlDbType.NVarChar, 100) { Value = displayName.Trim() });
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("The Trade database did not return the created MCP token.");
        }

        return new McpTokenCreated(
            reader.GetInt64(reader.GetOrdinal("TokenId")),
            reader.GetString(reader.GetOrdinal("DisplayName")),
            reader.GetString(reader.GetOrdinal("Token")),
            reader.GetString(reader.GetOrdinal("TokenPrefix")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("CreatedAt")),
            GetNullableDateTimeOffset(reader, "ExpiresAt"));
    }

    public async Task RevokeMcpTokenAsync(
        Guid portalUserId,
        long tokenId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, _options.RevokeMcpTokenProcedure, portalUserId);
        command.Parameters.Add(new SqlParameter("@TokenId", SqlDbType.BigInt) { Value = tokenId });
        await command.ExecuteNonQueryAsync(cancellationToken);
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

    private static SqlCommand CreateCommand(SqlConnection connection, string procedureName, Guid portalUserId)
    {
        var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = procedureName;
        command.CommandTimeout = 30;
        command.Parameters.Add(new SqlParameter("@PortalUserId", SqlDbType.UniqueIdentifier) { Value = portalUserId });
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
