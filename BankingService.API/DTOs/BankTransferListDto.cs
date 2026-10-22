namespace BankingService.API.DTOs
{
    public class BankTransferListDto
    {
        public List<BankTransferResponseDto> Data { get; set; } = [];

        public int Total { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}
