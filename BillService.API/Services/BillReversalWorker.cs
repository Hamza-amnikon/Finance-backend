using BillService.API.Data;
using BillService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BillService.API.Services
{
    public class BillReversalWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<BillReversalWorker> _logger;

        public BillReversalWorker(IServiceScopeFactory scopes, ILogger<BillReversalWorker> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Unable to check pending bill reversals.");
                }
                try
                {
                    if (!await timer.WaitForNextTickAsync(stoppingToken))
                    {
                        return;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        public async Task ProcessPendingAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopes.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillDbContext>();
            var ids = await context.Bills.AsNoTracking()
                .Where(row => row.Status == BillStatus.Cancelled && row.AccountingReversalPending)
                .OrderBy(row => row.Id).Select(row => row.Id).Take(50).ToListAsync(stoppingToken);
            foreach (var id in ids)
            {
                try
                {
                    using var itemScope = _scopes.CreateScope();
                    var itemContext = itemScope.ServiceProvider.GetRequiredService<BillDbContext>();
                    var bill = await itemContext.Bills.AsNoTracking().SingleAsync(row => row.Id == id, stoppingToken);
                    var service = itemScope.ServiceProvider.GetRequiredService<IBillService>();
                    await service.ChangeStatusAsync(id, BillStatus.Cancelled, bill.RowVersion, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Accounting reversal pending for cancelled bill {BillId}.", id);
                }
            }
        }
    }
}
