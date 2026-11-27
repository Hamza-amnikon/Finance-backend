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
                await MySqlTransactionLock.AcquireAsync(context, resource, cancellationToken);
                return;
            }
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 51000, 'Bank transaction processing is busy. Retry shortly.', 1;",
                cancellationToken
            );
        }
    }
}
