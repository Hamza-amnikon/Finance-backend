# Purchase Order Service

Standalone purchase-order API using the existing SparkFinance database. It reads existing vendor, branch, and currency masters directly; VendorService, ClientService, and sales APIs do not need to run.

## Run

Start PurchaseOrderService.API with the https launch profile. Swagger: https://localhost:7191/swagger/index.html. The frontend Vite proxy routes /api/purchase-orders to this port. Restart Vite after changing its proxy configuration.

Use the existing DefaultConnection in appsettings.json. Apply migrations explicitly with PurchaseOrderDbContext; this service never migrates or seeds automatically. Its migration history is purchases.__PurchaseOrderMigrationsHistory. Owned tables are purchases.PurchaseOrders, PurchaseOrderItems, and PurchaseOrderNumberCounters. Existing purchases.VendorMaster, sales.CurrencyMasters, and sales.BranchMasters are keyless read-only lookup mappings and are not managed by these migrations.

## Workflow

Select an active vendor, review branch/currency/address/payment terms, enter lines, preview totals, and save a draft. Only drafts can be edited. Issuing assigns the monthly PO number inside a serializable transaction. Drafts and issued orders can be cancelled. Issued/cancelled documents retain their snapshots. Orders do not create bills or payments yet.

Create requests require a unique RequestId; retries of the same payload return the existing order. Edits and status actions require the latest base64 RowVersion. Totals are calculated on the server using the currency master precision; tax applies after each line's monetary discount. The API validates active vendor/branch/currency again when issuing.

## Verification

Verified build, frontend focused lint/build, browser vendor selection/autofill/save/detail navigation, and SQL integration checks for preview, totals, idempotency, draft replacement, concurrency conflicts, issuing and cancellation. Database integration test orders were enclosed in a transaction and rolled back.
