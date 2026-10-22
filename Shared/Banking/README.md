> Disabled on 22 October 2026 at the user's request. The original payment forms,
> payment recording, partial-payment balances and void behavior have been restored
> from the pre-integration backups. Production projects no longer compile or call
> this helper. Applied nullable database columns and previous bank entries remain
> untouched to preserve recorded history. The notes below describe the inactive
> integration and must not be treated as current rollout instructions.

# Payment banking integration

Payments Made and invoice Payments Received now save a Posted bank movement inside the source payment's existing SQL transaction. The three services currently share the SparkFinance database. This implementation intentionally depends on that shared database: moving a service to a separate database requires replacing this boundary with a transactional outbox and authenticated Banking API. Do not point a payment service at a separate database containing copied banking tables.

## Behavior

- Issuing a bill or invoice does not move bank funds.
- New payment requests explicitly choose SettlementType Bank or Cash. Bank requires BankAccountId and BankAmount; Cash must omit both.
- Bank payments post Money Out for Payments Made and Money In for Payments Received. The account supplies company, currency and precision; never infer these from invoice branch names or payment mode text.
- For matching currencies the bank amount must equal the payment amount. For different currencies enter the actual debit/credit in the selected account currency. The original document currency amount still settles the bill/invoice. Bank fees are separate entries.
- The source payment's RequestId also identifies its bank entry. Retrying an unchanged request returns the same payment. Payment and ledger insertion commit together; failures roll both back. There is no pending sync state.
- Entries have readable payment number, vendor/client, document number and reference. Source payment responses contain the bank account snapshot and BankTransactionId.
- Existing payments keep null linkage. No historical balance changes or automatic backfill are performed.
- A linked vendor payment void requires BankReversalDate. It appends opposite Money In linked through ReversalOfTransactionId and restores the bill due; it does not cancel or refund anything at a real bank. Use only for confirmed cancellation/returned funds. Legacy unlinked voids retain their previous behavior.
- Invoice payment refund/void is not currently supported by this project's existing API.
- New automatic payment entries and vendor reversals reject dates in completed reconciliations. Reconciliation completion and these postings share a transaction-scoped account application lock. Existing manual transaction and transfer workflows retain their previous behavior; this feature does not retrofit their closed-period rules.
- This records confirmed bank movements. Cheques awaiting clearance should not be marked as a confirmed bank movement here; a separate outstanding-cheque workflow would be needed to model earlier cheque issue.

## Rollout

Back up the database as normal. Apply only these two additive migrations, then restart PaymentMadeService.API, SalesInvoiceService.API and BankingService.API together and reload the frontend. No Banking schema migration is needed.

From Visual Studio Package Manager Console:

```powershell
Update-Database -Migration LinkPaymentsMadeToBanking -Project PaymentMadeService.API -StartupProject PaymentMadeService.API -Context PaymentMadeDbContext
Update-Database -Migration LinkInvoicePaymentsToBanking -Project SalesInvoiceService.API -StartupProject SalesInvoiceService.API -Context SalesInvoiceDbContext
```

The migration scripts in each service's Database folder show the exact schema change. They do not insert payment or ledger rows. Do not apply the new frontend without updating and restarting both payment APIs.

## Verification

```powershell
dotnet run --project PaymentBanking.Tests -- --validation-only
dotnet run --project PaymentBanking.Tests
```

The second command needs LocalDB (or PAYMENT_BANKING_TEST_SERVER) and creates/drops a uniquely named FinancePaymentBankingChecks database. It never targets SparkFinance. A database connection failure is a blocked integration check, not a passing check.

In the frontend run:

```powershell
node --test "src/Admin/banking management/payment banking/paymentBanking.test.mjs"
```

After rollout, test using a test company/account and issued test documents in an open statement period: Bank payment decreases the selected balance once; Bank receipt increases it once; retry keeps the same bank entry; Cash creates no bank entry; overpayment/closed-date failure creates neither a new payment nor bank movement; vendor void adds one opposite entry and preserves the original. Reconcile against an independently obtained bank statement. Posting does not add rows to an already imported or completed statement.
