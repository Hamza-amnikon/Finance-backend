using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;

namespace SparkFinance.Database;

internal static class FinanceDatabase
{
    public const string MySqlProvider = "MySql.EntityFrameworkCore";

    public static bool IsFinanceMySql(this DatabaseFacade database) => database.ProviderName == MySqlProvider;

    public static DbContextOptionsBuilder UseFinanceDatabase(
        this DbContextOptionsBuilder options,
        IConfiguration configuration,
        string connectionString,
        Action<SqlServerDbContextOptionsBuilder>? configureSqlServer = null)
    {
        return (configuration["Database:Provider"] ?? "SqlServer") switch
        {
            "SqlServer" => options.UseSqlServer(connectionString, configureSqlServer),
            "MySql" => configuration.GetValue<bool>("Database:AllowMySqlWrites")
                ? options.UseMySQL(connectionString).AddInterceptors(MySqlRowVersionInterceptor.Instance)
                : options.UseMySQL(connectionString).AddInterceptors(MySqlReadOnly.Instance),
            _ => throw new InvalidOperationException("Configure Database:Provider as SqlServer or MySql.")
        };
    }

    public static void MapFinanceMySql(this ModelBuilder modelBuilder, DatabaseFacade database)
    {
        if (!database.IsFinanceMySql())
        {
            return;
        }

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.GetTableName() is string table)
            {
                entity.SetTableName(PhysicalName(entity.GetSchema(), table));
                entity.SetSchema(null);
            }

            if (entity.GetViewName() is string view)
            {
                entity.SetViewName(PhysicalName(entity.GetViewSchema(), view));
                entity.SetViewSchema(null);
            }

            foreach (var property in entity.GetDeclaredProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type == typeof(Guid))
                {
                    property.SetColumnType("char(36)");
                }
                else if (type == typeof(DateOnly))
                {
                    property.SetColumnType("date");
                    property.SetValueConverter(new ValueConverter<DateOnly, DateTime>(
                        value => value.ToDateTime(TimeOnly.MinValue), value => DateOnly.FromDateTime(value)));
                }
                else if (type == typeof(DateTimeOffset))
                {
                    property.SetColumnType("varchar(34)");
                    property.SetValueConverter(new ValueConverter<DateTimeOffset, string>(
                        value => value.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture),
                        value => DateTimeOffset.ParseExact(value, "yyyy-MM-dd HH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture)));
                }
                else if (type == typeof(byte[]) && property.IsConcurrencyToken)
                {
                    property.SetColumnType("binary(8)");
                    if (property.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate)
                    {
                        property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
                        property.SetAnnotation(MySqlModelMapping.ManagedRowVersion, true);
                    }
                }
            }
        }

        modelBuilder.HasDefaultSchema(null);
    }

    private static string PhysicalName(string? schema, string name) => $"{schema ?? "dbo"}__{name}";

    private sealed class MySqlReadOnly : SaveChangesInterceptor
    {
        public static readonly MySqlReadOnly Instance = new();

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
            throw new NotSupportedException("This service is configured for MySQL reads only.");

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This service is configured for MySQL reads only.");
    }
}
