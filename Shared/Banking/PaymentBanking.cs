using Microsoft.EntityFrameworkCore;

namespace Finance.Banking
{
    // All callers use the same Finance SQL database and their existing transaction.
    public static class PaymentBanking
    {
        public static async Task LockRequestAsync(
            DbContext context,
            Guid requestId,
            CancellationToken cancellationToken)
        {
            if (context.Database.CurrentTransaction == null)
            {
                throw new InvalidOperationException("Bank posting requires a payment transaction.");
            }

            var resource = "Finance.PaymentBanking.Request." + requestId;

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 51000, 'Payment request is busy. Retry this same request.', 1;",
                cancellationToken);
        }

        public static async Task LockAccountAsync(
            DbContext context,
            int accountId,
            CancellationToken cancellationToken)
        {
            if (context.Database.CurrentTransaction == null)
            {
                throw new InvalidOperationException("Bank posting requires a payment transaction.");
            }

            var resource = "Finance.PaymentBanking.Account." + accountId;

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 51000, 'Bank account is busy. Retry this same payment request.', 1;",
                cancellationToken);
        }

        public static void ValidateSettlement(
            string? settlementType,
            int? bankAccountId,
            decimal? bankAmount)
        {
            if (settlementType != "Bank" && settlementType != "Cash")
            {
                throw new ArgumentException("Select Bank or Cash for this payment.");
            }

            if (settlementType == "Cash")
            {
                if (bankAccountId != null || bankAmount != null)
                {
                    throw new ArgumentException("Cash payments cannot contain bank details.");
                }
            }
            else if (bankAccountId is null or <= 0 || bankAmount is null or <= 0 ||
                bankAmount > 99999999999999.9999m)
            {
                throw new ArgumentException("Select a bank account and enter its actual positive bank amount.");
            }
        }

        public static async Task<PaymentBankPosting?> PostAsync(
            DbContext context,
            string? settlementType,
            int? bankAccountId,
            decimal? bankAmount,
            string paymentCurrency,
            decimal paymentAmount,
            DateOnly date,
            int direction,
            Guid requestId,
            string reference,
            string description,
            CancellationToken cancellationToken,
            int? reversalOfTransactionId = null)
        {
            ValidateSettlement(settlementType, bankAccountId, bankAmount);

            if (settlementType == "Cash")
            {
                return null;
            }

            var accountId = bankAccountId!.Value;
            await LockAccountAsync(context, accountId, cancellationToken);

            var accounts = await context.Database.SqlQuery<PaymentBankAccount>(
                $"SELECT [Id], [CompanyId], [Name], [Currency], [CurrencyDecimalPlaces], [OpeningBalanceDate], [IsActive] FROM [expenses].[BankAccounts] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {accountId}")
                .ToListAsync(cancellationToken);

            var account = accounts.SingleOrDefault()
                ?? throw new ArgumentException("The selected bank account was not found.");

            if (!account.IsActive && reversalOfTransactionId == null)
            {
                throw new ArgumentException("Select an active bank account.");
            }

            if (account.OpeningBalanceDate == null || date < account.OpeningBalanceDate)
            {
                throw new ArgumentException("Payment date must be on or after the bank account opening date.");
            }

            var closed = await context.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS [Value] FROM [banking].[BankReconciliations] WITH (UPDLOCK,HOLDLOCK) WHERE [BankAccountId] = {accountId} AND [Status] = 'Completed' AND [PeriodEnd] >= {date}")
                .SingleAsync(cancellationToken);

            if (closed != 0)
            {
                throw new ArgumentException("This bank period is already reconciled. Use the actual bank movement date in an open period.");
            }

            if (account.CurrencyDecimalPlaces < 0 || account.CurrencyDecimalPlaces > 4 ||
                decimal.Round(bankAmount!.Value, account.CurrencyDecimalPlaces) != bankAmount)
            {
                throw new ArgumentException("Use the bank account currency's decimal precision.");
            }

            if (account.Currency == paymentCurrency && bankAmount != paymentAmount)
            {
                throw new ArgumentException("For the same currency, the bank amount must equal the payment amount. Record bank fees separately.");
            }

            if (direction != 1 && direction != 2)
            {
                throw new ArgumentException("Invalid bank transaction direction.");
            }

            var duplicate = await context.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS [Value] FROM [banking].[BankTransactions] WITH (UPDLOCK,HOLDLOCK) WHERE [RequestId] = {requestId}")
                .SingleAsync(cancellationToken);

            if (duplicate != 0)
            {
                throw new InvalidOperationException("This request ID is already linked to a bank entry. Check the original payment before submitting again.");
            }

            var now = DateTime.UtcNow;
            var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(description)));
            // Explicit SQL precision prevents four-decimal currencies from being rounded by parameter defaults.
            var amount = new Microsoft.Data.SqlClient.SqlParameter(
                "@bankAmount",
                System.Data.SqlDbType.Decimal)
            {
                Precision = 18,
                Scale = 4,
                Value = bankAmount.Value
            };
            var companyId = account.CompanyId;
            var currency = account.Currency;
            var precision = account.CurrencyDecimalPlaces;

            // OUTPUT is materialized before any LINQ composition. The caller commits both records.
            var ids = await context.Database.SqlQuery<int>(
                $"INSERT INTO [banking].[BankTransactions] ([RequestId], [RequestFingerprint], [BankAccountId], [CompanyId], [TransactionDate], [Direction], [Amount], [Currency], [CurrencyDecimalPlaces], [Status], [Reference], [Description], [Notes], [ReversalOfTransactionId], [CreatedDate], [PostedDate]) OUTPUT INSERTED.[Id] AS [Value] VALUES ({requestId}, {fingerprint}, {accountId}, {companyId}, {date}, {direction}, {amount}, {currency}, {precision}, 'Posted', {reference}, {description}, '', {reversalOfTransactionId}, {now}, {now})")
                .ToListAsync(cancellationToken);

            return new PaymentBankPosting
            {
                Id = ids.Single(),
                AccountName = account.Name,
                Currency = currency
            };
        }
    }

    public class PaymentBankPosting
    {
        public int Id { get; set; }
        public string AccountName { get; set; } = "";
        public string Currency { get; set; } = "";
    }

    public class PaymentBankAccount
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Name { get; set; } = "";
        public string Currency { get; set; } = "";
        public int CurrencyDecimalPlaces { get; set; }
        public DateOnly? OpeningBalanceDate { get; set; }
        public bool IsActive { get; set; }
    }
}
