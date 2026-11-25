using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace DashboardService.API.Services
{
    public class LocalDbKeepAliveWorker : BackgroundService
    {
        private readonly string _connectionString;
        private readonly ILogger<LocalDbKeepAliveWorker> _logger;

        public LocalDbKeepAliveWorker(
            string connectionString,
            ILogger<LocalDbKeepAliveWorker> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public static bool IsLocalDbConnection(string connectionString)
        {
            try
            {
                var settings = new SqlConnectionStringBuilder(connectionString);
                return settings.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        protected virtual DbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!IsLocalDbConnection(_connectionString))
            {
                return;
            }

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await using var connection = CreateConnection();
                        await connection.OpenAsync(stoppingToken);
                        _logger.LogInformation(
                            "Dashboard LocalDB connection is open while the API runs.");

                        while (!stoppingToken.IsCancellationRequested && connection.State == ConnectionState.Open)
                        {
                            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                            await using var command = connection.CreateCommand();
                            command.CommandText = "SELECT 1";
                            command.CommandTimeout = 5;
                            await command.ExecuteScalarAsync(stoppingToken);
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(
                            exception,
                            "Dashboard LocalDB connection was interrupted. The worker will retry.");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host shutdown can also cancel the retry delay after a failed connection.
            }
        }
    }
}
