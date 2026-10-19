using SalesEstimateService.API.Models;

namespace SalesEstimateService.API.DTOs
{
    public class SalesEstimateResponseDto
    {
        public int Id { get; set; }

        public Guid ClientId { get; set; }

        public string? EstimateNumber { get; set; }

        public string ClientName { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string BillingAddress { get; set; } = string.Empty;

        public DateOnly EstimateDate { get; set; }

        public DateOnly ValidUntil { get; set; }

        public DateOnly? ServicePeriodStart { get; set; }

        public DateOnly? ServicePeriodEnd { get; set; }

        public string Currency { get; set; } = string.Empty;

        public string Branch { get; set; } = string.Empty;

        public decimal SubTotal { get; set; }

        public decimal DiscountTotal { get; set; }

        public decimal TaxTotal { get; set; }

        public decimal GrandTotal { get; set; }

        public SalesEstimateStatus Status { get; set; }

        public string Notes { get; set; } = string.Empty;

        public string AdditionalTerms { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public DateTime? SentDate { get; set; }

        public DateTime? AcceptedDate { get; set; }

        public DateTime? DeclinedDate { get; set; }

        public DateTime? CancelledDate { get; set; }

        public int? ConvertedInvoiceId { get; set; }

        public DateTime? ConvertedDate { get; set; }

        public byte[] RowVersion { get; set; } = [];

        public List<SalesEstimateItemResponseDto> Items { get; set; } =
            new List<SalesEstimateItemResponseDto>();
    }
}
