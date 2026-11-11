# Bill Service

Follows ClientService.API conventions: block namespaces, explicit constructors, readonly dependency fields, one model/DTO per file, repositories, services, controllers, and ReactPolicy startup configuration.

## Start

Reload ClientService.API/SparkFinanceBackend.slnx to see all six projects. Run BillService.API using its https launch profile. Swagger: https://localhost:7193/swagger/index.html. The Vite frontend proxies /api/bills to this port. Restart Vite if its proxy configuration was not reloaded.

Uses the existing SparkFinance DefaultConnection. No other API process is required. Existing vendor, branch and currency tables are keyless read mappings, and PO conversion reads existing PO tables directly in the same database. No shared master tables are created or migrated.

## Flow

An issued PO has a Convert to Bill action. It copies all lines, rates, discounts, taxes and vendor snapshot into one linked draft bill. The PO is locked during conversion; a unique source-order index prevents duplicate bills. Repeating conversion returns the existing bill, including a cancelled bill. Partial or multiple billing of a PO is not supported in this release.

Edit the draft to enter the vendor invoice number, bill date and due date. Due dates are entered explicitly; payment terms are descriptive text and do not calculate dates. Linked bills retain the source vendor, branch and currency. Issuing requires an active vendor/branch/currency, current rowVersion, vendor invoice number, due date, and an issued source PO. It assigns a BILL/year/month/sequence number. Issued bills cannot be edited. Cancellation retains history. Vendor invoice numbers are unique per vendor among non-cancelled bills.

Manual draft bills, server total previews, list/search/status filters, details, printing and cancellation are also available. Bills do not create payments or update inventory. Payment balances and vendor credits remain future work.

## Database

Owned tables: purchases.Bills, purchases.BillItems, purchases.BillNumberCounters. Migration history: purchases.__BillMigrationsHistory. Migrations run explicitly and never automatically at API startup.

## Verification

Build and focused frontend lint/build. SQL integration checks cover draft/stale PO rejection, snapshot/totals, repeat conversion, required invoice details, bill editing/concurrency, numbering, duplicate invoice prevention and cancellation. Test PO and bill records were rolled back in one transaction. Browser tests use mocked data for conversion, edit, issue, mobile details and PDF print output.


## Company tax settings

Apply the BillDbContext migration `LinkBillTaxSettings` after the accounting `AddCompanyTaxSettings` migration. Restart BillService.API afterward. The bill migration adds the compatibility flag, nullable setting ID, and saved setting snapshot; it does not recalculate existing bills.

New direct bills and explicitly saved draft edits select configured tax per line using `/api/bills/tax-settings?branch=...&billDate=...`. Choices follow the branch's accounting company, country, active status, and inclusive effective dates. No percentages or country choices are supplied by the frontend. The server derives percentage and treatment, rejects stale selections and mixed treatments, and requires equal CGST/SGST components to match the existing posting calculation.

Issuance checks the selected setting against its stored snapshot. If the master changed, edit the draft, reload tax choices, reselect, and save before issuing. Previously issued bills retain their recorded tax. Legacy and purchase-order-converted bills keep their source values until explicitly edited; editing a previously taxable line requires choosing a saved setting or No tax. Purchase order tax selections now carry through conversion and are checked against the bill date and current version before a new conversion is saved. No-tax summary rows are hidden in preview and details.

Run `dotnet run --project BillTaxSettings.Tests` for isolated database checks, including preview, save, retry, draft edit, and issuance with simulated accounting. The temporary BillTaxTests_ database is deleted afterward; SparkFinance business records are not used as test records.


## Automatic accounting reversal on cancellation

Apply the `AddBillCancellationReversal` BillDbContext migration before restarting BillService.API. Rebuild and restart AccountingService.API and BillService.API together. The added boolean defaults to false and does not cancel, reverse, or change existing financial records.

Cancelling an issued bill first commits its cancelled status and durable `AccountingReversalPending` flag, then calls `POST /api/accounting-postings/purchase-bills/{billId}/reverse`. Accounting confirms the source bill is cancelled, reverses only its PurchaseBill journal, and reuses the original transaction amounts, base amounts and exchange rate. If no posting exists, no balancing journal is invented. Draft cancellation does not contact Accounting. Active payments and applied credits continue to block cancellation.

Completion clears the flag. An outage or lost response leaves it set. The bill details show the pending state and a Retry Accounting Reversal action; the backend retries batches every 30 seconds and catches up after restart. Until completion, ledger and tax balances may still contain the original posting. Reversal entries use the existing journal reversal date policy (current UTC date), so earlier reporting periods retain their historical entries. Historical cancelled bills with no pending flag are not retroactively reversed.

The source bill row lock serializes automated reversal requests. Existing reversals are reused, including manual corrections. Purchase bill posting now holds the same source bill lock and refuses cancelled sources, so delayed issue requests cannot recreate a posting after cancellation. Existing conversion, issuance, calculation, and manual journal reversal flows remain available.

Run `dotnet run --project BillCancellation.Tests` for temporary database checks covering normal cancellation, duplicate/concurrent retries, lost responses, Accounting outages, worker recovery, drafts, missing postings, old manual reversals, and payment/credit protections. Test records are not created in SparkFinance.
