namespace BankingService.API.DTOs
{
    public class BankReconciliationListDto
    {
        public List<BankReconciliationResponseDto> Data { get; set; } = [];

        public int Total { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}