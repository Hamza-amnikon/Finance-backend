namespace ExpenseService.API.DTOs
{
    public class ExpenseExchangeRateDto
    {
        public string RateBaseCurrency { get; set; } = "";

        public string RateQuoteCurrency { get; set; } = "";

        public decimal Rate { get; set; }

        public DateOnly RateDate { get; set; }

        public string Source { get; set; } = "";
    }
}
