using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SparkFinance.Database
{
    public static class MySqlModelMapping
    {
        public const string ManagedRowVersion = "MeritMySql:ManagedRowVersion";

        public static void ApplyVerifiedTableMappings(
            this ModelBuilder model,
            IReadOnlyDictionary<string, string> tableMappings)
        {
            var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entity in model.Model.GetEntityTypes())
            {
                var table = entity.GetTableName();
                var viewMapping = table == null;
                table ??= entity.GetViewName();
                if (table == null)
                {
                    continue;
                }
                var schema = viewMapping ? entity.GetViewSchema() : entity.GetSchema();
                var source = string.IsNullOrEmpty(schema) ? table : schema + "." + table;
                if (!tableMappings.TryGetValue(source, out var target) ||
                    string.IsNullOrWhiteSpace(target) || target.Length > 64 || target.Contains('\0'))
                {
                    throw new InvalidOperationException("Configure a verified MySQL table mapping for " + source + ".");
                }
                if (targets.TryGetValue(target, out var existing) && existing != source)
                {
                    throw new InvalidOperationException("Different source tables cannot share the MySQL table " + target + ".");
                }
                targets[target] = source;
                if (viewMapping)
                {
                    entity.SetViewSchema(null);
                    entity.SetViewName(target);
                }
                else
                {
                    entity.SetSchema(null);
                    entity.SetTableName(target);
                }
                foreach (var property in entity.GetProperties())
                {
                    if (property.ClrType == typeof(byte[]) && property.IsConcurrencyToken &&
                        property.ValueGenerated == ValueGenerated.OnAddOrUpdate)
                    {
                        property.ValueGenerated = ValueGenerated.Never;
                        property.SetColumnType("binary(8)");
                        property.SetAnnotation(ManagedRowVersion, true);
                    }
                }
            }
            model.Model.SetDefaultSchema(null);
        }
    }
}
