using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace SparkFinance.Database
{
    public static class FinancialRowLock
    {
        public static IQueryable<TEntity> LockForUpdate<TEntity>(
            this DbSet<TEntity> rows,
            DbContext context,
            string propertyName,
            object value) where TEntity : class
        {
            if (context.Database.CurrentTransaction == null)
            {
                throw new InvalidOperationException("A financial row lock requires an active database transaction.");
            }
            ArgumentNullException.ThrowIfNull(value);
            return rows.FromSqlRaw(BuildCommandText<TEntity>(context, propertyName), value);
        }

        public static string BuildCommandText<TEntity>(
            DbContext context,
            string propertyName) where TEntity : class
        {
            var entity = context.Model.FindEntityType(typeof(TEntity))
                ?? throw new InvalidOperationException("The locked entity is not part of this database model.");
            var table = entity.GetTableName();
            var viewMapping = table == null;
            table ??= entity.GetViewName();
            if (table == null)
            {
                throw new InvalidOperationException("A financial row lock requires a mapped table or view.");
            }
            var schema = viewMapping ? entity.GetViewSchema() : entity.GetSchema();
            var property = entity.FindProperty(propertyName)
                ?? throw new InvalidOperationException("The lock property is not mapped on this entity.");
            var store = viewMapping
                ? StoreObjectIdentifier.View(table, schema)
                : StoreObjectIdentifier.Table(table, schema);
            var column = property.GetColumnName(store)
                ?? throw new InvalidOperationException("The lock property is not mapped to a table column.");
            var helper = context.GetService<ISqlGenerationHelper>();
            var columnName = helper.DelimitIdentifier(column);
            var provider = context.Database.ProviderName;
            if (provider == "MySql.EntityFrameworkCore")
            {
                if (!string.IsNullOrEmpty(schema))
                {
                    throw new InvalidOperationException("Apply verified MySQL table mappings before locking financial rows.");
                }
                return $"SELECT * FROM {helper.DelimitIdentifier(table)} WHERE {columnName} = {{0}} FOR UPDATE";
            }
            if (provider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                return $"SELECT * FROM {helper.DelimitIdentifier(table, schema)} WITH (UPDLOCK,HOLDLOCK) WHERE {columnName} = {{0}}";
            }
            throw new NotSupportedException("Financial row locks are not configured for this database provider.");
        }
    }
}
