# Voiding an unposted received payment

POST /api/sales-invoices/payments/{paymentId}/void accepts a JSON body with a required reason.
It preserves the original amount, request ID, reference and row, and records Status=Voided,
VoidedDate (UTC) and VoidReason. Retries preserve the original cancellation.

The receipt and its existing bank drafts/links are cancelled in one transaction using the
banking worker's payment lock. Posted bank transactions, accounting journals/reversals and
already-transferred customer credit block cancellation. Posted receipts need a separate reviewed
reversal; this endpoint only voids unposted receipts.

Financial EF queries select Recorded payments by default. Lists, invoice history and request-ID
replay explicitly retain voided records. Invoice summaries, sales reports, customer statements,
customer-credit availability, aging, reconciliation and dashboard balances exclude voided receipts.
Historical cutoffs use current status; pre-void states are not reconstructed.

After applying AddReceivedPaymentVoiding, restart SalesInvoiceService.API, AccountingService.API,
BankingService.API and DashboardService.API. These model/controller changes require a full restart.
The Payments Received details dialog provides Void Unposted Payment with a reason and confirmation.
Voided receipts remain visible and cannot be printed as valid payment receipts.

Verification: ReceivedPaymentVoiding.Tests uses a unique temporary database and cleans it up.
The explicit --recover-authorized-six-test-payments mode is restricted to payments 2029-2034,
invoice 2029, USD 85 each, Cheque and TEST-INVALID references. It saves before/after checkpoints
under artifacts and checks row counts, existing bank drafts, journal absence and reconciliation.
It does not create replacement payments or delete history.
