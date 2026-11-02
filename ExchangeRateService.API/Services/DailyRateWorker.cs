namespace ExchangeRateService.API.Services
{
    public class DailyRateWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DailyRateWorker> _logger;

        public DailyRateWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<DailyRateWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            do
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var agent = scope.ServiceProvider
                        .GetRequiredService<DailyRateAgent>();

                    await agent.RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Daily exchange-rate check failed. Existing rates are retained; the worker will retry.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
