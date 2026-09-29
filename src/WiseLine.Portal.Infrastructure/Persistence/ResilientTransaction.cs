using System.Data;
using Microsoft.EntityFrameworkCore;

namespace WiseLine.Portal.Infrastructure.Persistence;

public static class ResilientTransaction
{
    public static async Task<TResult> ExecuteAsync<TResult>(
        PortalDbContext dbContext,
        Func<CancellationToken, Task<TResult>> operation,
        Func<TResult, bool> shouldCommit,
        CancellationToken cancellationToken = default,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                isolationLevel,
                cancellationToken);
            var result = await operation(cancellationToken);

            if (shouldCommit(result))
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return result;
        });
    }
}
