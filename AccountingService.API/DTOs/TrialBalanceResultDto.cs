namespace AccountingService.API.DTOs
{
    public class TrialBalanceResultDto
    {
        public int CompanyId { get; set; }

        public string CompanyName { get; set; }
            = string.Empty;

        public string Country { get; set; }
            = string.Empty;

        public string BaseCurrency { get; set; }
            = string.Empty;

        public DateOnly AsAtDate { get; set; }

        public string? Branch { get; set; }

        public string? CostCenter { get; set; }

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

        public decimal Difference { get; set; }

        public bool IsBalanced { get; set; }

        public IReadOnlyList<TrialBalanceLineDto>
            Data
        { get; set; }
                = Array.Empty<
                    TrialBalanceLineDto>();

        public int Total { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}