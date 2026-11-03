using AccountingService.API.DTOs;
using AccountingService.API.Models;

namespace AccountingService.API.Repositories
{
    public interface IGeneralLedgerRepository
    {
        Task<Account?> GetAccountAsync(
            int accountId,
            int companyId,
            CancellationToken cancellationToken);

        Task<decimal> GetOpeningBalanceAsync(
            GeneralLedgerQueryDto query,
            CancellationToken cancellationToken);

        Task<GeneralLedgerPageData> GetPageAsync(
            GeneralLedgerQueryDto query,
            CancellationToken cancellationToken);
    }

    public class GeneralLedgerPageData
    {
        public IReadOnlyList<GeneralLedgerDataRow> Rows
        {
            get;
            set;
        } = [];

        public int Total { get; set; }

        public decimal AmountBeforePage { get; set; }

        public decimal PeriodDebit { get; set; }

        public decimal PeriodCredit { get; set; }
    }

    public class GeneralLedgerDataRow
    {
        public int JournalEntryId { get; set; }

        public int JournalEntryLineId { get; set; }

        public int LineNumber { get; set; }

        public string EntryNumber { get; set; }
            = string.Empty;

        public DateOnly EntryDate { get; set; }

        public string Branch { get; set; }
            = string.Empty;

        public JournalEntrySource Source { get; set; }

        public JournalEntryStatus Status { get; set; }

        public string? SourceDocumentNumber { get; set; }

        public string? Reference { get; set; }

        public string Narration { get; set; }
            = string.Empty;

        public string Description { get; set; }
            = string.Empty;

        public string TransactionCurrency { get; set; }
            = string.Empty;

        public decimal ExchangeRate { get; set; }

        public decimal TransactionDebit { get; set; }

        public decimal TransactionCredit { get; set; }

        public decimal BaseDebit { get; set; }

        public decimal BaseCredit { get; set; }

        public string? CostCenter { get; set; }

        public string? CounterpartyType { get; set; }

        public string? CounterpartyId { get; set; }
    }
}