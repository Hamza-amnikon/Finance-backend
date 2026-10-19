namespace SalesEstimateService.API.DTOs
{
    public class SalesEstimateListResponseDto
    {
        public List<SalesEstimateResponseDto> Data { get; set; } =
            new List<SalesEstimateResponseDto>();

        public int Total { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}
