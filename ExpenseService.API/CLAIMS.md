# Expense Claims

The existing claim route and forms retain Draft → Submitted → Approved/Rejected → Paid.
Rejected claims return to Draft; Draft claims can be cancelled. Only drafts are editable.

## Run

Start ExpenseService.API using its existing HTTPS profile, AccountingService.API for
account choices, SalesInvoiceService.API for branch/currency choices, and
ExchangeRateService.API for foreign-currency approval and report conversion.
The frontend uses `/api/expense-claims`; its Vite proxy targets ExpenseService.API.

New databases need the `AddExpenseClaims` migration. It only adds ExpenseClaims,
ExpenseClaimReceipts, and ExpenseClaimHistory, with indexes and constraints.
Do not reset an existing database to install this module.

## Identity before authentication

Authentication remains a separate future module. In Development only, the frontend
provides an explicit local operator name and its existing role through headers.
These names support workflow testing and audit records; they are not verified users.
Use operator A to create/submit and operator B to approve. The owner cannot approve
their own claim, even with an Admin role. Production ignores local-operator headers
and requires an authenticated principal with a stable NameIdentifier/sub and Admin role.
Connect the future authentication middleware and frontend user ID to activate this path.
No users, passwords, or sign-in workflow were added.

## Accounting and reporting

Approval selects active expense and employee-payable posting accounts in the claim's
company. The shared rate service resolves the claim-date exchange rate to the company
currency. This rate is frozen at approval. Approval debits expense and credits the
selected payable. Paid selects a payment asset ledger account, date and reference;
it debits the payable and credits that asset at the frozen rate. This records a
reimbursement; it does not transfer funds or create a bank transaction.

The status change, audit record and balanced journal commit in one database transaction.
Row-version checks lock stale edits. Unique source-document keys and terminal Paid
status prevent duplicate reimbursement entries. Account types and company ownership
are checked on the server. No chart-of-account codes or exchange rates are assumed.

Expense Reports includes claims on approval date, and reimbursements on paid date.
Reimbursements never add expense again. An earlier report cutoff still shows the
claim unpaid if its payment date is after that cutoff. Claims have no separate tax
breakdown or due date, so their full amount is cost and no claim overdue amount is inferred.

Receipts accept PDF, PNG, or JPEG signatures, up to ten attachments of 5 MB each.
Files are stored in the database, locked after submission, and downloaded as attachments.

## Verification

`dotnet run --project ExpenseClaims.Tests --configuration Claims -p:UseAppHost=false`

The integration runner creates and deletes its own uniquely named LocalDB database.
It never writes test claims, journals or payments to SparkFinance.
