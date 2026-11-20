# Internal Payment Requests — stage 1

Stage 1 adds the request backend and database migration. Stage 2 adds the Banking sidebar page, MUI list, draft editor, request details and permission-aware action confirmations. Printing, completed-transfer linking and received/remaining calculation are implemented in stage 3.

## Configuration

BankingService.API/appsettings.json: InternalPaymentRequests:DisplayName controls the document name.
Services:AccountingService:BaseUrl is reused for verified company permissions. No company IDs, bank IDs, roles, currencies, or user identities are embedded in the feature.

## Migration

Migration: AddInternalPaymentRequests.
The migration creates banking.InternalPaymentRequests and banking.InternalPaymentRequestLines and registers banking.internal-payment-requests in the permission catalog only when absent.
It does not modify security.RolePermissions or bank-transfer tables.
Review all pending migrations before applying with dotnet ef database update --project BankingService.API.
The generated incremental SQL file AddInternalPaymentRequests.sql contains only this stage's migration.
No live database migration was executed during implementation.

## API

Base route: /api/internal-payment-requests
- GET ?page=1 — paged requests visible to either participating company; optional companyId, status and search filters run before pagination.
- GET /setup — configurable display name, verified company action permissions, company-name directory and permitted active receiving banks.
- GET /{id} — authorized request and receiving-bank summary; hidden IDs return 404.
- POST — create draft with a unique RequestId, company IDs, receiving bank, dates, notes, purpose/amount lines.
- PUT /{id} — update a draft using its current RowVersion.
- POST /{id}/submit — submit; requires Edit permission in the requesting company.
- POST /{id}/approve or /reject — requires Approve permission in the paying company; reject requires a reason.
- POST /{id}/cancel — cancels a Draft or Submitted request, requiring Edit permission in the requesting company.
Action payload: RowVersion (base64 JSON) and optional Note.

Currency and decimal precision are taken from the selected active receiving bank; the server computes the total. Amounts are never automatically rounded.
Both authorization and version checks run on the backend. Local admin is verified through the existing session-check path.
Approval creates no payment, bank transaction, sales invoice, or journal.

## Printing and completed-transfer linking

Migration LinkInternalPaymentRequestTransfers adds banking.InternalPaymentRequestTransfers only. Apply it after AddInternalPaymentRequests; the incremental SQL is LinkInternalPaymentRequestTransfers.sql. No live database migration was executed during implementation.

- GET /{id}/print requires View plus Export in either participating company and returns the configured document title, company names, requested purpose amounts, receiving-bank details, approval record and linked transfers.
- GET /{id}/transfer-candidates?page=1 requires Edit on the paying company's request permission and View on the paying company's Bank Transfers permission. It returns only completed, unlinked transfers that match the paying company, requesting company, receiving bank, currency and remaining balance.
- POST /{id}/transfers accepts BankTransferId and the request RowVersion. It associates the whole destination receipt once. It never changes a bank-transfer row or creates a bank transaction.

The caller is revalidated by AccountingService for both permission routes. Export IDs come from saved CanExport assignments; no role/company IDs are embedded in the feature.

ReceivedAmount sums destination amounts from linked Completed transfers. RemainingAmount and FundingStatus (Awaiting payment, Partially Paid, Paid) are derived without changing the persisted approval status. Draft, pending and cancelled transfers do not count. Request/transfer locks, row versions and a unique BankTransferId index protect against overfunding, repeat submissions and concurrent double allocation. A successful identical retry returns the existing result.

Rebuild/restart AccountingService and BankingService after deploying this stage. Existing company assignments are not changed or auto-granted. Local admin retains existing verified access.

## Verification and remaining browser checks

InternalPaymentRequestLinks.Tests creates a fresh generated LocalDB database, tests permissions, decimal totals, partial/full funding, overfunding, idempotent retries and concurrent links, confirms bank-transfer rows are untouched, and deletes only that generated database.

Run: dotnet run --project InternalPaymentRequestLinks.Tests

Pending browser verification after migration/service restart: print preview and Save PDF with long requests; authorized and forbidden print actions; matching completed transfer selection; partial/full receipts; unchanged ordinary transfer creation/confirmation; and role/company isolation in separate admin/Microsoft tabs.

## Rule tests

dotnet run --project InternalPaymentRequests.Tests

Tests cover company action boundaries, payer approval, invalid transitions, rejection reasons, exact decimal totals, and invalid amounts.

## Request ownership and approval permissions

Apply PermissionService AddRequestCancelPermission and BankingService AddRequestActionAudit before deploying these binaries. Both additive migrations were applied to SparkFinance after a verified COPY_ONLY backup on 2026-11-20. Existing grants were preserved; CanCancel defaults to false.

Restart PermissionService, AccountingService and BankingService, then refresh MERIT. In Role Permissions enable Show Finance actions to see Cancel. Give the raiser View/Create/Edit for the requesting company. Give approvers View/Approve for the paying company. Export governs printing; Cancel in either participating company governs cancellation of Draft/Submitted requests. Do not grant Cancel to a raiser who should only submit.

Only the creator with Edit may edit/submit a draft. No account, including local admin, may approve/reject its own request. Multiple authorized approvers may see Submitted requests; the existing row-version check makes the first successful decision final. Submission/cancellation actor, UTC time and note are stored separately from approval/rejection audit details. Existing audit values are not fabricated.

Browser verification: use separate authenticated accounts; submit as raiser, verify no Approve/Reject/Cancel; approve as a permitted second user; verify wrong-company and View-only accounts cannot act; try concurrent decisions from two approvers and expect one success and one conflict. Confirm existing completed-transfer linking is unchanged.
