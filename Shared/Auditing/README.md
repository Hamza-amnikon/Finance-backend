# Finance Audit History

AccountingService.API owns the append-only audit API (`/api/audit-history`) and the
`audit.Events` table in the existing finance database. No audit edit/delete HTTP
endpoints are provided. The existing frontend page is Settings → Audit History.

## Capture

`AuditedDbContext` wraps EF saves used by all 13 API projects. It takes safe original
and current financial values, saves the transaction, records the audit rows on the
same database connection/transaction, and commits them together. An existing caller
transaction remains responsible for commit/rollback. Generated record keys are
resolved after the save. Concurrency failures and rolled-back transactions do not
leave successful-change logs. No-op saves add no events.

Known direct-SQL transaction paths are explicitly instrumented: shared payment bank
postings, received-payment bank settlement repair, and claim journals. Direct database
edits made outside these application paths are not monitored. This is application
audit history, not a reconstruction of every SQL operation.

Selected financial fields are recorded. File bytes, descriptions/notes, addresses,
bank account numbers, credentials, tokens and row versions are excluded. A change
to an excluded property records its property name, not its contents. Extend the
explicit field allowlist only after checking that it contains no sensitive data.

Before this migration is installed, saves continue without audit capture so existing
module-only databases remain usable. Once installed, audit writes join the business
transaction: a failed audit write rolls that save back. Audit API requests return a
clear setup error if its table is missing.

## Installation and restart

Apply AccountingService.API's `AddAuditHistory` migration after checking prior pending
migrations. It creates the audit table/indexes and reads a baseline of supported
existing records into Snapshot events; it does not update their financial data.
Snapshot events are timestamped when captured and have no Before value or invented
actor. Their source record dates remain fields in the snapshot.

Rebuild and restart all backend services to activate capture throughout the app.
Restart Vite after the new audit proxy configuration. Old service binaries will not
record their later writes. Services still use their original repositories, status
rules, numbering and calculations; no triggers are installed.

## Identity and access before the authentication module

An authenticated NameIdentifier/sub is used when available. A declared claim operator
is accepted only in Development and labelled `Local operator (unverified)`.
Requests without verified identity are `Unattributed`; non-request background work
is `System`. The fixed frontend Admin display name is never treated as proof of identity.

The API requires Admin role. In Development only, the current frontend can send its
local role in `X-Audit-Role` for testing. Production rejects that bypass and requires
an authenticated Admin; connect the future authentication middleware to activate it.
Database administrators still control database permissions; no tamper-proof storage
claim is made.

## Query and export

Date/service/type/branch/action/search filters are server-side. Branch filtering
matches explicitly recorded branches; company-level setup records may have no branch.
Dates are sent as local-midnight boundaries converted to UTC, matching the browser's
display. Results paginate at 25 by default (100 maximum) and are ordered by event ID.
CSV exports the same filters, includes before/after snapshots and actor evidence, and
requires filters that yield at most 10,000 events. Formula-like text is escaped.

## Tests

`dotnet run --project AuditHistory.Tests --configuration Audit -p:UseAppHost=false`

The integration runner creates and deletes only its own uniquely named LocalDB
database. Tests cover baseline accuracy, cross-service capture, generated keys,
excluded fields, update/delete snapshots, rollback, stale updates, role checks,
filters, pagination, export and local identity labels.
