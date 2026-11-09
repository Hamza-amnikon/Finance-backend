using AccountingService.API.DTOs;

namespace AccountingService.API.Services
{
    public static class ReconciliationRules
    {
        public static bool Equal(
            decimal expected,
            decimal actual,
            int precision
        ) => decimal.Round(actual - expected, precision, MidpointRounding.AwayFromZero) == 0m;
        public static ReconciliationCheckDto Posting(
            ReconciliationDocumentRow row,
            int? precision
        )
        {
            var result = new ReconciliationCheckDto
            {
                Id = $"posting-{row.Kind}-{row.Id}",
                Group = "Posting completeness",
                Title = row.Kind,
                Reference = row.Number,
                Currency = row.Currency,
                Expected = row.Total,
                Link = row.Kind == "Sales invoice" ? $"/sales/invoices?invoiceId={row.Id}" : row.Kind == "Purchase bill" ? $"/purchases/bills/{row.Id}" : null
            };
            if (row.PostingCount == 0)
            {
                result.Status = "Issue";
                result.Explanation = "No corresponding accounting journal dated on or before the cutoff was found.";
            }
            else if (row.PostingCount > 1)
            {
                result.Status = "Issue";
                result.Explanation = "Multiple journals use the same source reference. Investigate possible duplicate posting.";
            }
            else if (row.PostedCount != 1)
            {
                result.Status = "Issue";
                result.Explanation = "The source has a journal, but its current status is not Posted.";
            }
            else if (!string.Equals(row.PostingCurrency, row.Currency, StringComparison.OrdinalIgnoreCase) && (row.Kind is "Customer payment" or "Vendor payment"))
            {
                result.Status = "Review";
                result.Explanation = "A converted settlement journal exists. Compare its bank, control-account, fees and exchange-gain/loss lines; original and settlement totals cannot be compared directly.";
            }
            else if (!string.Equals(row.PostingCurrency, row.Currency, StringComparison.OrdinalIgnoreCase))
            {
                result.Status = "Issue";
                result.Explanation = "Source and accounting-journal currencies differ.";
            }
            else if (!precision.HasValue)
            {
                result.Status = "Review";
                result.Explanation = "Currency precision is missing from the currency master; the amount comparison is unavailable.";
            }
            else
            {
                result.Actual = row.PostingTotal;
                result.Difference = row.PostingTotal - row.Total;
                result.Status = row.PostingTotal.HasValue && Equal(row.Total, row.PostingTotal.Value, precision.Value) ? "Matched" : "Issue";
                result.Explanation = result.Status == "Matched" ? "One posted source journal was found and its document-currency total agrees. Account allocation still requires review." : "The accounting-journal total differs from the source total.";
                if (result.Status == "Issue" && (row.Kind is "Customer payment" or "Vendor payment"))
                {
                    result.Status = "Review";
                    result.Explanation = "Payment journal totals require a settlement, fee and FX bridge. The difference is not treated as a proven posting error.";
                }
            }

            return result;
        }

        public static ReconciliationCheckDto Bank(
            ReconciliationBankRow row,
            int companyId,
            string currency,
            int precision
        )
        {
            var result = new ReconciliationCheckDto
            {
                Id = $"bank-{row.Id}",
                Group = "Banking",
                Title = "Bank register vs ledger",
                Reference = row.Name,
                Currency = currency,
                Link = "/banking/reconciliation"
            };
            if (!row.AccountingAccountId.HasValue || row.LedgerCompanyId != companyId || row.LedgerType != "Asset" || row.IsPostingAccount != true)
            {
                result.Status = "Issue";
                result.Explanation = "The bank account needs a valid asset posting ledger belonging to the same company.";
            }
            else if (row.MappingCount != 1)
            {
                result.Status = "Review";
                result.Explanation = "Multiple bank accounts share this ledger. Separate the mappings or reconcile their combined register balances.";
            }
            else if (!string.Equals(row.Currency, currency, StringComparison.OrdinalIgnoreCase))
            {
                result.Status = "Not comparable";
                result.Actual = row.LedgerBalance;
                result.Explanation = $"The bank register is {row.Currency} ({row.RegisterBalance}); the ledger is {currency}. A historical-rate/FX bridge is required. No current reference rate is substituted.";
            }
            else
            {
                result.Expected = row.RegisterBalance;
                result.Actual = row.LedgerBalance;
                result.Difference = row.LedgerBalance - row.RegisterBalance;
                result.Status = Equal(row.RegisterBalance, row.LedgerBalance, precision) ? "Matched" : "Review";
                result.Explanation = "Compares bank opening balance plus posted register movements with the mapped ledger through the cutoff. Differences may include missing postings, opening balances or manual journals.";
            }

            return result;
        }
    }
}
