# SparkFinance backend

## Branch master table

Branches are stored in `amnikon.Branches`. SalesInvoiceService.API owns the
`MoveBranchesToAmnikon` migration; the other APIs use the same branch rows through
read-only mappings or queries. The move preserves branch IDs, company links,
invoice prefixes, saved details, identity values and row versions.

Apply the migration before restarting the updated services. It keeps
`sales.BranchMasters` as a compatibility synonym pointing to `amnikon.Branches`
so older running services use the same physical rows during deployment. It is
not a second table. Do not insert or copy another set of branch records.

The reviewed idempotent deployment script is `tools/MoveBranchesToAmnikon.sql`.
It assumes the preceding `AddBranchInvoiceNumberPrefix` migration is applied.
`BranchTableMigration.Tests` checks data preservation, old/new name access,
concurrency and rollback against a disposable database.

The backend contains 12 independently running ASP.NET Core APIs targeting .NET 10.
They use the existing SparkFinance database on `(localdb)\MSSQLLocalDB`.

## Run in Visual Studio

1. Open `SparkFinanceBackend.slnx` in the repository root.
2. Stop previous debugging sessions before rebuilding.
3. In the solution startup settings, select multiple startup projects and choose
   **Start** for the APIs you need. The full frontend uses all 12 APIs.
4. Use the `https` launch profile where available; ClientService uses `http`.

## Run one API from a terminal

From the repository root:

```powershell
dotnet build SparkFinanceBackend.slnx
dotnet run --project ClientService.API --launch-profile http --no-build
```

For other APIs, use their project name and `--launch-profile https` in separate
terminals. Press Ctrl+C to stop each API before rebuilding. Running `dotnet run`
in the repository root without `--project` does not select an API.

## Troubleshoot startup

- **MSB3021 / MSB3027 / file used by another process:** an earlier API instance
  is holding its build output open. Stop that instance (Visual Studio Stop
  Debugging or Ctrl+C in its terminal), then rebuild. Avoid launching the same
  API from both Visual Studio and a terminal.
- **Address already in use:** another instance already owns the configured port.
  Stop that instance before launching another copy.
- **Swagger opens but data requests fail:** check the LocalDB instance using
  `SqlLocalDB info MSSQLLocalDB`, then `SqlLocalDB start MSSQLLocalDB` if stopped.
  The tool may need its full path, for example
  `C:\Program Files\Microsoft SQL Server\170\Tools\Binn\SqlLocalDB.exe`.
- If LocalDB reports **Stopped** while its SQL Server process is still running,
  the instance may have stale state. Close database clients and restart that
  instance normally before retrying. Do not delete or recreate the instance or
  its database files to address a startup problem.

Database migrations are separate from starting the APIs. Do not reset or migrate
the existing database merely to resolve process locks or LocalDB startup errors.

## Daily exchange-rate agent

`ExchangeRateService.API` provides the read-only Exchange Rates page and shared
rates for Dashboard, Banking and Expenses. Include it as a startup project using
its `https` profile (7441/7442). See [setup and verification](ExchangeRateService.API/README.md).
The default daily check is 19:30 India time (14:00 UTC), with hourly retries.
Daily checks require the API to be running; no external scheduler is installed.

## Organization profiles

Organization profiles use `SalesInvoiceService.API` through `/api/organization-profiles` for paginated lists, detail, create and update. Apply `SalesInvoiceService.API/Database/AddOrganizationProfiles.sql` to an existing deployment before enabling the page; the script only creates the table if absent. Restart the API after deploying the controller and restart Vite after changing proxy configuration.

The profile uses active currencies from `sales.CurrencyMasters`. An unchanged saved currency is preserved when it later becomes inactive. Profiles store general contact information and do not modify accounting companies, branch invoice details, or their tax registration numbers.

## Company Tax Settings

Tax Settings uses AccountingService.API at `/api/tax-settings`. Apply the accounting EF migration `AddCompanyTaxSettings` using the normal migration process; it creates `accounting.TaxTypes` and `accounting.TaxRates` and installs supported type definitions, without default percentages. The standalone `AccountingService.API/Database/AddTaxSettings.sql` is a reference/setup script for isolated checks; do not apply it before EF migration on an EF-managed deployment.

Company and country come from `expenses.Companies`. Supported definitions are database records: AE VAT, IN combined CGST/SGST, and IN IGST, mapped to existing treatment identifiers. Component labels are also loaded from the database. Users maintain company-specific component percentages and effective dates. Total rates are calculated server-side; overlapping versions of the same code are rejected even when inactive. Saved versions use optimistic concurrency, and deactivation preserves the record. New directly created sales invoices select these saved settings per service line. Existing invoice and bill values are preserved; purchase bills are also connected through BillService.API, and estimates and purchase orders carry saved selections through their conversion flows.

Run `dotnet run --project TaxSettings.Tests` for isolated API checks; the randomly named FinanceTaxTests_ database and test server are removed afterward. Restart AccountingService.API and Vite after deployment to load new endpoints and proxy routes. Authentication is still the existing application-wide integration task; this feature does not introduce a new identity provider.

## Sales invoice tax settings connection

Apply the SalesInvoiceService.API EF migration `LinkInvoiceTaxSettings`. It adds nullable setting references, a saved setting snapshot, and a legacy compatibility flag without recalculating existing invoices. Restart SalesInvoiceService.API after deployment.

New invoice choices use the branch accounting-company link, company country, active status, and invoice date. The backend reads the configured percentage and derives the treatment; submitted percentages cannot override a selected master setting. CGST/SGST must have equal components to match the existing posting calculation. No tax remains an explicit option. Changing branch or invoice date clears selections. A changed setting blocks issuance of a new draft until it is recreated with valid settings; issued and legacy invoices retain their recorded tax values.

Run `dotnet run --project InvoiceTaxSettings.Tests` for isolated integration checks. The randomly named InvoiceTaxTests_ database is removed afterward. This connection covers new direct sales invoices. Purchase bills have their own connection documented in BillService.API/README.md; accepted-estimate conversion now preserves and validates its saved selection.

## Estimate and purchase order tax settings

SalesEstimateService.API and PurchaseOrderService.API expose company/date-scoped tax choices at `/api/sales-estimates/tax-settings` and `/api/purchase-orders/tax-settings`. New documents and explicitly saved draft edits select a saved setting per line instead of supplying a percentage. Empty selection means No tax. Selections are cleared when the managing company/branch or source date changes. Stale versions and mixed treatments are rejected, and source documents store the chosen snapshot.

Apply `LinkEstimateTaxSettings` with SalesEstimateDbContext and `LinkPurchaseOrderTaxSettings` with PurchaseOrderDbContext after the accounting tax master migration. Both add selection, treatment, and compatibility fields without recalculating existing amounts; old rows default to the legacy path. Stop debugging, rebuild, and restart SalesEstimateService.API, PurchaseOrderService.API, SalesInvoiceService.API, and BillService.API after deployment; constructor and model changes require a full restart.

Accepted-estimate conversion copies the saved setting, derived treatment and original amounts to the invoice. Issued-purchase-order conversion copies them to the bill. A setting must still match its saved version, company and destination document date before a new conversion is created. A mismatch stops conversion with an explanation; it never silently substitutes a new rate. Existing conversion retries recover the original destination document. Historical source documents retain their recorded values; explicitly editing an old taxable draft requires selecting a saved setting or No tax.

Run `dotnet run --project TaxDocumentFlows.Tests` for isolated end-to-end service/database checks covering both flows, Dubai VAT, Turbhe CGST/SGST and IGST, no-tax documents, duplicate conversion prevention, invalid destination dates, changed settings and legacy compatibility. The TaxFlowTests_ database is deleted afterward. These checks simulate remote client/currency/estimate calls; browser display still needs a normal user acceptance check. Existing direct-document checks remain in InvoiceTaxSettings.Tests and BillTaxSettings.Tests.


## Fixed and percentage line discounts

Estimates, invoices, purchase orders and bills support `DiscountType` (`Fixed` or `Percentage`) and nullable `DiscountValue` on each line. The backend calculates monetary `DiscountAmount`, rounds it using the document currency precision, and then taxes the remaining line amount. Percentage values are limited to 0-100 with at most four decimal places. Fixed deductions cannot exceed the line subtotal. Disabling a discount produces zero deduction.

All four APIs use the shared calculation in Shared/Discounts/LineDiscount.cs. Calculated monetary discount amounts remain available to reports, credit calculations and accounting. Request `DiscountAmount` remains compatible with earlier fixed-amount clients when `DiscountValue` is omitted; new forms send the chosen type and raw value. This prevents repeated calculations or edits from confusing a percentage with its monetary result.

Apply each API's `AddLineDiscountChoices` migration. The migrations default old lines to `Fixed`, leave raw value nullable for the legacy monetary fallback, and do not recalculate existing totals. Rebuild and restart SalesEstimateService.API, SalesInvoiceService.API, PurchaseOrderService.API and BillService.API before using the updated frontend. Both conversions copy the type, raw value and monetary amount; editing uses the raw value and recalculates for the current quantity/price.

Verification is included in TaxDocumentFlows.Tests: all four calculators, fixed and percentage values, legacy fallback, 0/100 percent boundaries, invalid inputs, currency rounding, repeated calculation, draft edits and both conversion flows. Frontend helper and payload checks are in src/live/lineDiscount.test.js in finance-frontend. Browser layout and user acceptance checks remain manual.
