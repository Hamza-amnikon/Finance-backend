using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SparkFinance.Database
{
    public sealed class MySqlRowVersionInterceptor : SaveChangesInterceptor
    {
        public static MySqlRowVersionInterceptor Instance { get; } = new();

        private static void RefreshVersions(DbContext? context)
        {
            if (context == null)
            {
                return;
            }
            foreach (var entry in context.ChangeTracker.Entries()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
            {
                foreach (var property in entry.Properties)
                {
                    if (property.Metadata.FindAnnotation(MySqlModelMapping.ManagedRowVersion)?.Value is not true)
                    {
                        continue;
                    }
                    // Preserve OriginalValue: EF must still compare the previously read token.
                    property.CurrentValue = RandomNumberGenerator.GetBytes(8);
                    if (entry.State == EntityState.Modified)
                    {
                        property.IsModified = true;
                    }
                }
            }
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            RefreshVersions(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RefreshVersions(eventData.Context);
            return ValueTask.FromResult(result);
        }
    }
}
