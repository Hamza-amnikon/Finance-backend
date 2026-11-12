using System.ComponentModel.DataAnnotations;
using AccountingService.API.Data;
using AccountingService.API.DTOs;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Services
{
    public class ReconciliationReportService : IReconciliationReportService
    {
        private readonly AccountingDbContext _context;
        public ReconciliationReportService(AccountingDbContext context)
        {
            _context = context;
        }

        public async Task<ReconciliationReportDto> GetAsync(
            int companyId,
            DateOnly asOf,
            CancellationToken cancellationToken
        )
        {
            if (companyId < 1 || asOf == default || asOf > DateOnly.FromDateTime(DateTime.Today))
            {
                throw new ValidationException("Select a company and a cutoff date on or before today.");
            }

            var company = await _context.Companies.AsNoTracking().SingleOrDefaultAsync(row => row.Id == companyId, cancellationToken) ?? throw new KeyNotFoundException("Accounting company was not found.");
            var precisionRows = await _context.Database.SqlQueryRaw<CurrencyPrecisionRow>("SELECT CurrencyCode AS Currency, DecimalPlaces AS Precision FROM sales.CurrencyMasters").ToListAsync(cancellationToken);
            var precisions = precisionRows.Where(row => row.Precision is >= 0 and <= 4).ToDictionary(row => row.Currency, row => row.Precision, StringComparer.OrdinalIgnoreCase);
            if (!precisions.TryGetValue(company.Currency, out var precision))
            {
                throw new ValidationException("Configure the company currency and its decimal precision in the currency master.");
            }

            SqlParameter[] Parameters() => [new("@company", companyId), new("@asOf", asOf.ToDateTime(TimeOnly.MinValue)), new("@nextDay", asOf.AddDays(1).ToDateTime(TimeOnly.MinValue))];
            var report = new ReconciliationReportDto
            {
                CompanyId = companyId,
                CompanyName = company.Name,
                Currency = company.Currency,
                AsOf = asOf
            };
            const string sourceSql = @"
                WITH sources AS
                (
                    SELECT i.Id, N'Sales invoice' AS Kind, COALESCE(i.InvoiceNumber,N'') AS Number, i.Currency,
                        i.GrandTotal AS Total, N'SalesInvoice' AS Source, CONVERT(nvarchar(100),i.Id) AS SourceId
                    FROM sales.SalesInvoices i WHERE i.Status=N'Issued' AND i.InvoiceDate<=@asOf
                        AND EXISTS(SELECT 1 FROM amnikon.Branches b WHERE b.Name=i.Branch AND b.AccountingCompanyId=@company)
                    UNION ALL
                    SELECT c.Id,N'Sales credit note',COALESCE(c.CreditNoteNumber,N''),c.Currency,c.GrandTotal,N'SalesCreditNote',CONVERT(nvarchar(100),c.Id)
                    FROM sales.SalesCreditNotes c INNER JOIN sales.SalesInvoices i ON i.Id=c.SalesInvoiceId
                    WHERE c.Status=N'Issued' AND c.CreditNoteDate<=@asOf AND EXISTS(SELECT 1 FROM amnikon.Branches b WHERE b.Name=i.Branch AND b.AccountingCompanyId=@company)
                    UNION ALL
                    SELECT p.Id,N'Customer payment',CONCAT(N'Payment ',p.Id),p.Currency,p.Amount,N'PaymentReceived',CONVERT(nvarchar(100),p.Id)
                    FROM sales.SalesInvoicePayments p INNER JOIN sales.SalesInvoices i ON i.Id=p.SalesInvoiceId
                    WHERE p.Status=N'Recorded' AND p.PaymentDate<=@asOf AND EXISTS(SELECT 1 FROM amnikon.Branches b WHERE b.Name=i.Branch AND b.AccountingCompanyId=@company)
                    UNION ALL
                    SELECT b.Id,N'Purchase bill',COALESCE(b.BillNumber,N''),b.Currency,b.GrandTotal,N'PurchaseBill',CONVERT(nvarchar(100),b.Id)
                    FROM purchases.Bills b WHERE b.Status=N'Issued' AND b.BillDate<=@asOf
                        AND EXISTS(SELECT 1 FROM amnikon.Branches s WHERE s.Name=b.Branch AND s.AccountingCompanyId=@company)
                    UNION ALL
                    SELECT p.Id,N'Vendor payment',p.PaymentNumber,p.Currency,p.Amount,N'PaymentMade',CONVERT(nvarchar(100),p.Id)
                    FROM purchases.PaymentsMade p WHERE p.Status=N'Recorded' AND p.PaymentDate<=@asOf
                        AND EXISTS(SELECT 1 FROM amnikon.Branches b WHERE b.Name=p.Branch AND b.AccountingCompanyId=@company)
                    UNION ALL
                    SELECT c.Id,N'Vendor credit',COALESCE(c.VendorCreditNumber,N''),c.Currency,c.GrandTotal,N'VendorCredit',CONVERT(nvarchar(100),c.Id)
                    FROM purchases.VendorCredits c WHERE c.Status=N'Issued' AND c.CreditDate<=@asOf
                        AND EXISTS(SELECT 1 FROM amnikon.Branches b WHERE b.Name=c.Branch AND b.AccountingCompanyId=@company)
                    UNION ALL
                    SELECT e.Id,N'Recorded expense',e.Reference,e.Currency,e.Amount,N'Expense',CONVERT(nvarchar(100),e.Id)
                    FROM expenses.Expenses e WHERE e.CompanyId=@company AND e.Status=N'Recorded' AND e.Date<=@asOf
                    UNION ALL
                    SELECT c.Id,N'Claim approval',c.Reference,c.Currency,c.Amount,N'Expense',CONCAT(N'Claim:',c.Id,N':approval')
                    FROM expenses.ExpenseClaims c WHERE c.CompanyId=@company AND c.Status IN(N'Approved',N'Paid') AND c.ApprovedDate<=@asOf
                    UNION ALL
                    SELECT c.Id,N'Claim reimbursement',c.Reference,c.Currency,c.Amount,N'Expense',CONCAT(N'Claim:',c.Id,N':payment')
                    FROM expenses.ExpenseClaims c WHERE c.CompanyId=@company AND c.Status=N'Paid' AND c.PaidDate<=@asOf
                )
                SELECT s.Id,s.Kind,s.Number,s.Currency,s.Total,p.PostingCount,p.PostedCount,p.PostingCurrency,p.PostingTotal
                FROM sources s OUTER APPLY
                (SELECT COUNT(*) AS PostingCount,COALESCE(SUM(CASE WHEN j.Status=N'Posted' THEN 1 ELSE 0 END),0) AS PostedCount,
                    MAX(j.Currency) AS PostingCurrency,MAX(j.TotalDebit) AS PostingTotal
                    FROM accounting.JournalEntries j WHERE j.CompanyId=@company AND j.Source=s.Source AND j.SourceDocumentId=s.SourceId AND j.EntryDate<=@asOf) p";
            var documents = await _context.Database.SqlQueryRaw<ReconciliationDocumentRow>(sourceSql, Parameters()).ToListAsync(cancellationToken);
            report.Checks.AddRange(documents.Select(row => ReconciliationRules.Posting(row, precisions.TryGetValue(row.Currency, out var digits) ? digits : null)));
            const string journalSql = @"
                SELECT j.Id,j.EntryNumber AS Number,j.Currency,j.TotalDebit,j.TotalCredit,
                    COALESCE(SUM(l.DebitAmount),0) AS LineDebit,COALESCE(SUM(l.CreditAmount),0) AS LineCredit,
                    COALESCE(SUM(l.BaseDebitAmount),0) AS BaseDebit,COALESCE(SUM(l.BaseCreditAmount),0) AS BaseCredit,COUNT(l.Id) AS LineCount,
                    COALESCE(SUM(CASE WHEN l.Id IS NOT NULL AND (l.CompanyId<>j.CompanyId OR a.CompanyId<>j.CompanyId OR a.Id IS NULL) THEN 1 ELSE 0 END),0) AS OwnershipErrors
                FROM accounting.JournalEntries j LEFT JOIN accounting.JournalEntryLines l ON l.JournalEntryId=j.Id
                LEFT JOIN accounting.Accounts a ON a.Id=l.AccountId
                WHERE j.CompanyId=@company AND j.EntryDate<=@asOf AND j.Status IN(N'Posted',N'Reversed')
                GROUP BY j.Id,j.EntryNumber,j.Currency,j.TotalDebit,j.TotalCredit";
            var journals = await _context.Database.SqlQueryRaw<ReconciliationJournalRow>(journalSql, Parameters()).ToListAsync(cancellationToken);
            if (documents.Count == 0 && journals.Count == 0)
            {
                report.Checks.Add(new ReconciliationCheckDto { Id = "empty-coverage", Group = "Ledger controls", Title = "Data coverage", Status = "Review", Explanation = "No eligible source documents or posted journals exist through this cutoff. Zero balances do not establish posting completeness." });
            }

            foreach (var journal in journals)
            {
                var known = precisions.TryGetValue(journal.Currency, out var digits);
                var valid = known && journal.LineCount >= 2 && journal.OwnershipErrors == 0 && ReconciliationRules.Equal(journal.TotalDebit, journal.TotalCredit, digits) && ReconciliationRules.Equal(journal.TotalDebit, journal.LineDebit, digits) && ReconciliationRules.Equal(journal.TotalCredit, journal.LineCredit, digits) && ReconciliationRules.Equal(journal.BaseDebit, journal.BaseCredit, precision);
                report.Checks.Add(new ReconciliationCheckDto { Id = $"journal-{journal.Id}", Group = "Journal integrity", Title = "Journal headers, lines and ownership", Reference = journal.Number, Currency = company.Currency, Expected = 0, Actual = journal.BaseDebit - journal.BaseCredit, Difference = journal.BaseDebit - journal.BaseCredit, Status = valid ? "Matched" : known ? "Issue" : "Review", Explanation = $"{journal.LineCount} lines; {journal.OwnershipErrors} account/company ownership errors. Checks header/line agreement and balance in document and company currencies.", Link = "/accounting/journal-entries" });
            }

            report.Checks.Add(new ReconciliationCheckDto { Id = "trial-balance", Group = "Ledger controls", Title = "Posted trial balance", Currency = company.Currency, Expected = 0, Actual = journals.Sum(row => row.BaseDebit - row.BaseCredit), Difference = journals.Sum(row => row.BaseDebit - row.BaseCredit), Status = ReconciliationRules.Equal(0, journals.Sum(row => row.BaseDebit - row.BaseCredit), precision) ? "Matched" : "Issue", Explanation = "Base debits less base credits through the cutoff. A balanced trial balance alone does not prove posting completeness.", Link = "/accounting/trial-balance" });
            const string balanceSql = @"
                SELECT N'Receivables' AS Kind,i.GrandTotal-COALESCE(p.Paid,0)-COALESCE(c.Credits,0)-COALESCE(a.Applied,0) AS Balance,j.ExchangeRate AS BookingRate
                FROM sales.SalesInvoices i
                OUTER APPLY(SELECT SUM(Amount) AS Paid FROM sales.SalesInvoicePayments WHERE Status=N'Recorded' AND SalesInvoiceId=i.Id AND PaymentDate<=@asOf)p
                OUTER APPLY(SELECT SUM(GrandTotal) AS Credits FROM sales.SalesCreditNotes WHERE SalesInvoiceId=i.Id AND Status=N'Issued' AND CreditNoteDate<=@asOf)c
                OUTER APPLY(SELECT SUM(Amount) AS Applied FROM sales.CustomerCreditApplications WHERE TargetInvoiceId=i.Id AND AppliedAtUtc<@nextDay)a
                OUTER APPLY(SELECT MAX(ExchangeRate) AS ExchangeRate FROM accounting.JournalEntries WHERE CompanyId=@company AND Source=N'SalesInvoice' AND SourceDocumentId=CONVERT(nvarchar(100),i.Id) AND Status IN(N'Posted',N'Reversed') AND EntryDate<=@asOf)j
                WHERE i.Status=N'Issued' AND i.InvoiceDate<=@asOf AND EXISTS(SELECT 1 FROM amnikon.Branches b WHERE b.Name=i.Branch AND b.AccountingCompanyId=@company)
                UNION ALL
                SELECT N'Payables',b.GrandTotal-COALESCE(p.Paid,0)-COALESCE(a.Applied,0),j.ExchangeRate
                FROM purchases.Bills b
                OUTER APPLY(SELECT SUM(Amount) AS Paid FROM purchases.PaymentsMade WHERE BillId=b.Id AND Status=N'Recorded' AND PaymentDate<=@asOf)p
                OUTER APPLY(SELECT SUM(Amount) AS Applied FROM purchases.VendorCreditApplications WHERE BillId=b.Id AND Status=N'Applied' AND ApplicationDate<=@asOf)a
                OUTER APPLY(SELECT MAX(ExchangeRate) AS ExchangeRate FROM accounting.JournalEntries WHERE CompanyId=@company AND Source=N'PurchaseBill' AND SourceDocumentId=CONVERT(nvarchar(100),b.Id) AND Status IN(N'Posted',N'Reversed') AND EntryDate<=@asOf)j
                WHERE b.Status=N'Issued' AND b.BillDate<=@asOf AND EXISTS(SELECT 1 FROM amnikon.Branches s WHERE s.Name=b.Branch AND s.AccountingCompanyId=@company)";
            var balances = await _context.Database.SqlQueryRaw<ReconciliationBalanceRow>(balanceSql, Parameters()).ToListAsync(cancellationToken);
            const string controlSql = @"
                SELECT a.SystemKey AS Kind,COUNT(DISTINCT a.Id) AS AccountCount,
                    COALESCE(SUM(CASE WHEN j.Id IS NOT NULL THEN l.BaseDebitAmount-l.BaseCreditAmount ELSE 0 END),0) AS Balance
                FROM accounting.Accounts a LEFT JOIN accounting.JournalEntryLines l ON l.AccountId=a.Id
                LEFT JOIN accounting.JournalEntries j ON j.Id=l.JournalEntryId AND j.CompanyId=@company AND j.EntryDate<=@asOf AND j.Status IN(N'Posted',N'Reversed')
                WHERE a.CompanyId=@company AND a.SystemKey IN(N'ACCOUNTS_RECEIVABLE',N'ACCOUNTS_PAYABLE') GROUP BY a.SystemKey";
            var controls = await _context.Database.SqlQueryRaw<ControlRow>(controlSql, Parameters()).ToListAsync(cancellationToken);
            foreach (var kind in new[]
            {
                "Receivables",
                "Payables"
            }

            )
            {
                var rows = balances.Where(row => row.Kind == kind).ToList();
                var missing = rows.Count(row => !row.BookingRate.HasValue || row.BookingRate <= 0);
                var control = controls.SingleOrDefault(row => row.Kind == (kind == "Receivables" ? "ACCOUNTS_RECEIVABLE" : "ACCOUNTS_PAYABLE"));
                decimal? expected = missing == 0 ? rows.Sum(row => decimal.Round(row.Balance * row.BookingRate!.Value, precision, MidpointRounding.AwayFromZero)) : null;
                decimal? actual = control == null ? null : control.Balance * (kind == "Payables" ? -1 : 1);
                report.Checks.Add(new ReconciliationCheckDto { Id = $"control-{kind}", Group = "Ledger controls", Title = $"{kind}: source balances vs control ledger", Currency = company.Currency, Expected = expected, Actual = actual, Difference = actual - expected, Status = expected.HasValue && actual.HasValue && ReconciliationRules.Equal(expected.Value, actual.Value, precision) ? "Matched" : "Review", Explanation = $"Diagnostic at original invoice/bill booking rates, including net overpayments. {missing} documents lack booking rates; {control?.AccountCount ?? 0} control accounts found. Differences need an FX, manual-adjustment and unallocated-credit bridge; they are not auto-corrected." });
            }

            const string bankSql = @"
                SELECT b.Id,b.Name,b.Currency,b.AccountingAccountId,a.CompanyId AS LedgerCompanyId,a.Type AS LedgerType,a.IsPostingAccount,
                    (SELECT COUNT(*) FROM expenses.BankAccounts x WHERE x.CompanyId=b.CompanyId AND x.AccountingAccountId=b.AccountingAccountId) AS MappingCount,
                    CASE WHEN b.OpeningBalanceDate IS NULL OR b.OpeningBalanceDate<=@asOf THEN b.OpeningBalance ELSE 0 END + COALESCE(t.Movement,0) AS RegisterBalance,
                    COALESCE(l.Balance,0) AS LedgerBalance,r.PeriodEnd AS LastCompletedDate,
                    (SELECT COUNT(*) FROM banking.BankStatementLines s WHERE s.BankReconciliationId=r.Id AND s.MatchedBankTransactionId IS NULL) AS UnmatchedStatementLines
                FROM expenses.BankAccounts b LEFT JOIN accounting.Accounts a ON a.Id=b.AccountingAccountId
                OUTER APPLY(SELECT SUM(CASE WHEN Direction=1 THEN Amount ELSE -Amount END) AS Movement FROM banking.BankTransactions WHERE BankAccountId=b.Id AND CompanyId=@company AND Status=N'Posted' AND TransactionDate<=@asOf)t
                OUTER APPLY(SELECT SUM(x.BaseDebitAmount-x.BaseCreditAmount) AS Balance FROM accounting.JournalEntryLines x INNER JOIN accounting.JournalEntries j ON j.Id=x.JournalEntryId WHERE x.AccountId=b.AccountingAccountId AND j.CompanyId=@company AND j.EntryDate<=@asOf AND j.Status IN(N'Posted',N'Reversed'))l
                OUTER APPLY(SELECT TOP(1) q.Id,q.PeriodEnd FROM banking.BankReconciliations q WHERE q.BankAccountId=b.Id AND q.CompanyId=@company AND q.Status=N'Completed' AND q.PeriodEnd<=@asOf ORDER BY q.PeriodEnd DESC,q.Id DESC)r
                WHERE b.CompanyId=@company";
            var banks = await _context.Database.SqlQueryRaw<ReconciliationBankRow>(bankSql, Parameters()).ToListAsync(cancellationToken);
            foreach (var bank in banks)
            {
                report.Checks.Add(ReconciliationRules.Bank(bank, companyId, company.Currency, precision));
                report.Checks.Add(new ReconciliationCheckDto { Id = $"statement-{bank.Id}", Group = "Banking", Title = "Completed statement coverage", Reference = bank.Name, Status = bank.UnmatchedStatementLines > 0 ? "Issue" : bank.LastCompletedDate == asOf ? "Matched" : "Review", Explanation = bank.LastCompletedDate.HasValue ? $"Last completed statement ends {bank.LastCompletedDate:yyyy-MM-dd}; {bank.UnmatchedStatementLines} unmatched statement lines. Later register activity still needs reconciliation." : "No completed statement was found through the cutoff.", Link = "/banking/reconciliation" });
            }

            var claimRisks = await _context.Database.SqlQuery<ClaimRiskRow>($@"SELECT c.Id,c.Reference,c.PaymentAccountId,a.Type,a.SystemKey,p.SystemKey AS ParentKey,
                (SELECT COUNT(*) FROM expenses.BankAccounts b WHERE b.CompanyId=c.CompanyId AND b.AccountingAccountId=c.PaymentAccountId) AS BankMappings
                FROM expenses.ExpenseClaims c LEFT JOIN accounting.Accounts a ON a.Id=c.PaymentAccountId LEFT JOIN accounting.Accounts p ON p.Id=a.ParentAccountId
                WHERE c.CompanyId={companyId} AND c.Status=N'Paid' AND c.PaidDate<={asOf}").ToListAsync(cancellationToken);
            foreach (var claim in claimRisks)
            {
                var valid = claim.Type == "Asset" && (claim.SystemKey == "CASH_ON_HAND" || claim.ParentKey == "BANK_ACCOUNTS" || claim.BankMappings > 0);
                report.Checks.Add(new ReconciliationCheckDto { Id = $"claim-bank-{claim.Id}", Group = "Claims", Title = "Claim reimbursement payment route", Reference = claim.Reference, Status = valid ? "Review" : "Issue", Explanation = valid ? "A cash/bank-related ledger is selected. Claim reimbursement does not itself create a linked bank-register transaction; verify register/statement evidence and avoid posting a duplicate expense." : "The selected payout ledger is not identified as cash or a mapped bank account. Review its classification and payment evidence.", Link = "/expenses/claims" });
            }

            return report;
        }

        public class CurrencyPrecisionRow
        {
            public string Currency { get; set; } = "";
            public int Precision { get; set; }
        }

        public class ControlRow
        {
            public string Kind { get; set; } = "";
            public int AccountCount { get; set; }
            public decimal Balance { get; set; }
        }

        public class ClaimRiskRow
        {
            public int Id { get; set; }
            public string Reference { get; set; } = "";
            public int? PaymentAccountId { get; set; }
            public string? Type { get; set; }
            public string? SystemKey { get; set; }
            public string? ParentKey { get; set; }
            public int BankMappings { get; set; }
        }
    }
}
