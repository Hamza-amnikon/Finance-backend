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
