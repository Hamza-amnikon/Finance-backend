using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MySql.Data.MySqlClient;

namespace SparkFinance.Database
{
    public static class MySqlDatabaseConfiguration
    {
        public static DbContextOptionsBuilder UseMeritMySql(
            this DbContextOptionsBuilder options,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MySqlConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Configure ConnectionStrings:MySqlConnection in User Secrets or server configuration.");
            }

            MySqlConnectionStringBuilder connection;
            try
            {
                connection = new MySqlConnectionStringBuilder(connectionString);
            }
            catch (ArgumentException)
            {
                throw new InvalidOperationException("MySqlConnection must contain a valid MySQL connection string.");
            }
            if (string.IsNullOrWhiteSpace(connection.Server) || string.IsNullOrWhiteSpace(connection.Database))
            {
                throw new InvalidOperationException("MySqlConnection requires a server and database.");
            }

            // Financial writes keep their explicit transaction and idempotency handling.
            // Automatic retries cannot be enabled until ambiguous commits are verified.
            options.UseMySQL(connection.ConnectionString);
            options.ReplaceService<IModelCacheKeyFactory, MySqlModelCacheKeyFactory>();
            options.AddInterceptors(MySqlRowVersionInterceptor.Instance);
            return options;
        }
    }
}
