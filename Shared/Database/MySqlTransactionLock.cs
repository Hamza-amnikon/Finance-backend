using Microsoft.EntityFrameworkCore;

namespace SparkFinance.Database;

public static class MySqlTransactionLock
{
    public static async Task AcquireAsync(DbContext context, string resource, CancellationToken cancellationToken)
    {
        if (context.Database.ProviderName != "MySql.EntityFrameworkCore")
        {
            throw new NotSupportedException("This transaction lock requires MySQL.");
        }
        if (context.Database.CurrentTransaction == null)
        {
            throw new InvalidOperationException("A transaction lock requires an active transaction.");
        }
        if (string.IsNullOrWhiteSpace(resource) || resource.Length > 255)
        {
            throw new ArgumentException("The transaction lock name is invalid.", nameof(resource));
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT IGNORE INTO finance__OperationLocks (`Name`) VALUES ({resource})",
            cancellationToken);
        var locked = await context.Database.SqlQueryRaw<string>(
            "SELECT `Name` AS Value FROM finance__OperationLocks WHERE `Name` = {0} FOR UPDATE",
            resource).ToListAsync(cancellationToken);
        if (locked.Count != 1)
        {
            throw new InvalidOperationException("The MySQL transaction lock could not be acquired.");
        }
    }
}
