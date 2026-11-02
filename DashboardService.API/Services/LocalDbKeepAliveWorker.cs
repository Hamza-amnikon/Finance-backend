using System.Data;
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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var connection = new SqlConnection(_connectionString);

                try
                {
                    await connection.OpenAsync(stoppingToken);
                    _logger.LogInformation(
                        "Dashboard database connection is open; LocalDB will remain available while the API runs.");

                    while (!stoppingToken.IsCancellationRequested &&
                        connection.State == ConnectionState.Open)
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
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Dashboard database connection was interrupted. The API will reconnect automatically.");
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
        }
    }
}
