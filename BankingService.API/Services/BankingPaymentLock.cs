using BankingService.API.Data;
using Microsoft.EntityFrameworkCore;
using SparkFinance.Database;

namespace BankingService.API.Services
{
    public static class BankingPaymentLock
    {
        public static async Task AcquireAsync(
            BankingDbContext context,
            string key,
            CancellationToken cancellationToken
        )
        {
            var resource = "Finance.AutomaticPayment." + key;
            if (context.Database.IsFinanceMySql())
            {
                if (context.Database.CurrentTransaction == null)
                {
                    throw new InvalidOperationException("Bank transaction locking requires an active transaction.");
                }
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT IGNORE INTO finance__OperationLocks (`Name`) VALUES ({resource})",
                    cancellationToken);
                var locked = await context.Database.SqlQueryRaw<string>(
                    "SELECT `Name` AS Value FROM finance__OperationLocks WHERE `Name` = {0} FOR UPDATE",
                    resource).ToListAsync(cancellationToken);
                if (locked.Count != 1)
                {
                    throw new InvalidOperationException("Bank transaction lock could not be acquired.");
                }
                return;
            }
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 51000, 'Bank transaction processing is busy. Retry shortly.', 1;",
                cancellationToken
            );
        }
    }
}
