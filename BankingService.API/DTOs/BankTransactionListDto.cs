namespace BankingService.API.DTOs
{
    public class BankTransactionListDto
    {
        public List<BankTransactionResponseDto> Data { get; set; } = [];

        public int Total { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}