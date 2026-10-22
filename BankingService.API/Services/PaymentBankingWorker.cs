namespace BankingService.API.Services
{
    public class PaymentBankingWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<PaymentBankingWorker> _logger;

        public PaymentBankingWorker(IServiceScopeFactory scopes, ILogger<PaymentBankingWorker> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            do
            {
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<PaymentBankingService>().RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Automatic payment banking failed. Saved payments are unchanged; processing will retry.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
