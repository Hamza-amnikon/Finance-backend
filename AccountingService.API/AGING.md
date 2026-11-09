# Receivables and Payables Aging

`GET /api/aging-reports?asOf=YYYY-MM-DD` is a read-only query against the existing
sales and purchases tables. No database migration or transaction-workflow change
is required. The frontend route is `/reports/aging`, under Reports.

Start AccountingService.API for the report and company/account settings,
SalesInvoiceService.API for branch settings, and ExchangeRateService.API for
reporting-currency conversion. Restart the frontend after proxy changes.

The cutoff includes all currently issued invoices and bills dated on or before
the selected date. Receivables subtract dated payments, issued invoice credit
notes, and applied customer credits. Payables subtract recorded bill payments
and applied vendor credits. Unallocated credits do not offset other documents.
Only positive outstanding balances are returned. Original amounts are preserved.

The frontend groups by due date: not overdue (including due today), 1–30, 31–60,
61–90, over 90 days, and no due date. Date differences use UTC calendar days.
Customer/vendor, branch, bucket, and search filters apply consistently to summary,
details, and CSV. Document links open the existing invoice and bill screens.

Branches and reporting-currency defaults come from saved accounting-company
links; rates come from the same shared service as the other reports. Reference
rates are requested at the cutoff. Saved-rate fallback is disclosed with source
and actual rate date, and included in CSV metadata. No numeric rates are embedded.

Historical reports reconstruct dated settlements using current document/payment
statuses. They do not reconstruct documents or payments that were subsequently
voided, because the source modules do not provide complete status history.
Customer credit transfer cutoffs use UTC dates. Employee claim payables are outside
this customer/vendor report and remain in Expense Reports.

Tests:

`dotnet run --project AgingReports.Tests --configuration Aging -p:UseAppHost=false`

The integration runner creates and deletes only its own uniquely named LocalDB
database. It never inserts sample data into SparkFinance.
