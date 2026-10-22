namespace BankingService.API.DTOs
{
    public class BankAccountResponseDto
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }

        public string CompanyName { get; set; } = "";

        public string Name { get; set; } = "";

        public string BankName { get; set; } = "";

        public string LastFour { get; set; } = "";

        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        public decimal OpeningBalance { get; set; }

        public DateOnly? OpeningBalanceDate { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }
}