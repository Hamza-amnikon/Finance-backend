# Accounting Reconciliation and Posting Completeness

Frontend: Reports → Accounting Reconciliation (`/reports/reconciliation`).
API: `GET /api/reconciliation-reports?companyId=...&asOf=YYYY-MM-DD`.
The module reads the existing shared finance database through AccountingService.API.
It does not save, post, reverse, reimburse or repair records, and adds no migration.

## Checks

- Source posting coverage for issued invoices, sales credits, bills, vendor credits,
  customer/vendor payments, recorded expenses, and approved/paid claims. A matching
  source journal must be posted and dated on/before the cutoff. Original-currency
  totals are compared only when meaningful. Converted or fee-bearing settlement
  differences require a review rather than an automatic error classification.
- Journal headers vs lines, debit/credit balance in document and company currencies,
  minimum line counts, and account/company ownership. A balanced trial balance can
  coexist with missing documents or incorrect journal lines, so these are separate.
- AR/AP controls vs net source balances at original invoice/bill booking rates.
  Missing booking rates leave Expected unavailable, never zero. These are diagnostics:
  manual journals, different payment-rate policies, unallocated credits and currency
  movements require a bridge. A difference is Review, not an instruction to alter data.
- Bank register opening balance plus posted movements vs its mapped company ledger.
  Mapping ownership, posting-account classification, and duplicate mappings are checked.
  Foreign-currency registers are Not comparable to base-currency ledger amounts without
  a historical/FX bridge. No current reference rate is used to force equality.
- Latest completed bank-statement coverage and unmatched statement lines. This does
  not reproduce all outstanding-item calculations in Bank Reconciliation.
- Paid-claim payout-account classification and the existing lack of an automatic
  linked bank-register entry. The report flags it without posting duplicate expense.

Company and currency settings are loaded from the existing master data. Amount
comparisons use configured currency precision. A company with no eligible source
documents/journals receives an explicit data-coverage review instead of a clean bill
of health. Filters and search apply to counts, details and CSV.

## Interpretation

Matched: this individual check agrees; not an overall accounting certification.
Issue: a missing posting, malformed journal or invalid mapping was identified.
Review: investigate timing, setup, exchange differences or supporting evidence.
Not comparable: the amounts cannot safely be compared in a common basis.

The cutoff uses current source/document/payment statuses and dated settlements.
Later voids are not reconstructed. Customer-credit applications use UTC cutoffs.
Archived branches remain in company-level checks through their existing company links.
Unmapped source branches need master-data review outside a selected-company scope.
The report is not an automatic correction, period-closing or revaluation process.

## Run and test

Rebuild/restart AccountingService.API and restart the frontend after proxy changes.
Other transaction services retain their existing workflows. Company/currency choices
use the existing Accounting API; no transaction-service mutation is called.

`dotnet run --project ReconciliationReports.Tests --configuration Reconciliation -p:UseAppHost=false`

The tests create deliberate errors in a uniquely named temporary database and remove
only that database. They never insert test data into SparkFinance. Frontend tests
cover filters/counts, required inputs, zero vs unavailable amounts and CSV escaping.
