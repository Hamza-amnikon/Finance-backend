namespace BankingService.API.DTOs
{
    public class BankAccountListDto
    {
        public List<BankAccountResponseDto> Data { get; set; } = [];

        public int Total { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}