namespace ExpenseService.API.Models
{
    public class ExpenseCurrencyLookup
    {
        public string CurrencyCode { get; set; } = "";

        public string CurrencyName { get; set; } = "";

        public int DecimalPlaces { get; set; }

        public bool IsActive { get; set; }
    }
}
