namespace AccountingService.API.DTOs
{
    public class TrialBalanceLineDto
    {
        public int AccountId { get; set; }

        public string AccountCode { get; set; }
            = string.Empty;

        public string AccountName { get; set; }
            = string.Empty;

        public string AccountType { get; set; }
            = string.Empty;

        public string NormalBalance { get; set; }
            = string.Empty;

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

        public decimal DebitBalance { get; set; }

        public decimal CreditBalance { get; set; }
    }
}