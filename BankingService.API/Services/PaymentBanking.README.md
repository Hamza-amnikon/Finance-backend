# Automatic banking from original payments

The original payment APIs and React forms are unchanged. BankingService polls committed payments every five seconds and stores a unique link per source module/payment id. Each bank transaction and its link commit together. Worker errors never roll back or block the original payment.

Routes are configured in appsettings.json under PaymentBanking. Dubai uses the configured AED account; Turbhe, India and Mumbai use the configured INR account. Names are resolved to exactly one active account. The payment amount, not the bill or invoice total, is used. Cash creates no bank entry; configured confirmed electronic methods post, and cheques create Draft entries until clearance is confirmed. Unknown branches, missing/ambiguous bank accounts, unsupported methods, and currency mismatches become NeedsReview. No exchange rate is invented.

The activation cutoff is stored once in banking.PaymentBankingSettings by migration AddBranchPaymentBanking. Only payments created after activation are considered. Existing payments, prior test bank entries, and original balances are not backfilled or reversed. Stop/restart preserves the cutoff and safely catches missed new payments.

Payments in closed bank periods produce Draft entries with their original dates and a review note. The original payment is still saved normally. Posting these automatic drafts is prevented inside closed periods. Reconciliation completion uses the same account transaction lock. Existing manual transfer behavior is unchanged.

Voiding a new vendor payment cancels its unposted bank draft or appends a single opposite bank entry if already posted. A reversal in a closed period stays Draft. Voiding is a Finance correction, not a real-bank refund. Customer refunds/voids are not part of the original customer payment API.

GET /api/bank-transactions/payment-sync exposes payments requiring review and the activation timestamp. Draft transactions and their notes appear in the existing Banking Transactions page. Changing routing configuration requires restarting BankingService.

The previous Shared/Banking/PaymentBanking.cs helper remains inactive; it is not used by this implementation.

Run the database checks with dotnet run --project BankingService.Tests. BANKING_TEST_SERVER can specify a SQL instance; otherwise LocalDB is used. Checks create/drop a randomly named FinanceBranchBankingChecks_ database and never write test payments to SparkFinance.
