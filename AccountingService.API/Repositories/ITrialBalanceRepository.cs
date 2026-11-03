using AccountingService.API.DTOs;
using AccountingService.API.Models;

namespace AccountingService.API.Repositories
{
    public interface ITrialBalanceRepository
    {
        Task<AccountingCompanyLookup?>
            GetCompanyAsync(
                int companyId,
                CancellationToken cancellationToken);

        Task<TrialBalancePageData>
            GetPageAsync(
                TrialBalanceQueryDto query,
                CancellationToken cancellationToken);
    }

    public class TrialBalancePageData
    {
        public IReadOnlyList<TrialBalanceDataRow>
            Rows
        { get; set; }
                = [];

        public int Total { get; set; }

        public decimal TotalDebitMovement
        {
            get;
            set;
        }

        public decimal TotalCreditMovement
        {
            get;
            set;
        }

        public decimal TotalDebitBalance
        {
            get;
            set;
        }

        public decimal TotalCreditBalance
        {
            get;
            set;
        }
    }

    public class TrialBalanceDataRow
    {
        public int AccountId { get; set; }

        public string AccountCode { get; set; }
            = string.Empty;

        public string AccountName { get; set; }
            = string.Empty;

        public AccountType AccountType { get; set; }

        public AccountNormalBalance NormalBalance
        {
            get;
            set;
        }

        public int? ParentAccountId { get; set; }

        public string? ParentAccountCode
        {
            get;
            set;
        }

        public string? ParentAccountName
        {
            get;
            set;
        }

        public bool IsControlAccount { get; set; }

        public bool IsSystemAccount { get; set; }

        public decimal DebitMovement { get; set; }

        public decimal CreditMovement { get; set; }

        public decimal DebitBalance
        {
            get;
            set;
        }

        public decimal CreditBalance
        {
            get;
            set;
        }
    }
}