using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SparkFinance.Database
{
    public sealed class MySqlModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
        {
            var mappings = context is IMySqlTableMappingContext mappedContext
                ? JsonSerializer.Serialize(mappedContext.MySqlTableMappings.OrderBy(mapping => mapping.Key, StringComparer.Ordinal))
                : string.Empty;

            return (context.GetType(), mappings, designTime);
        }
    }
}
