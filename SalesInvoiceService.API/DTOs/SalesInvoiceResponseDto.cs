using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.DTOs
{
    public class SalesInvoiceResponseDto
    {
        public int Id { get; set; }

        public Guid ClientId { get; set; }

        public string? InvoiceNumber { get; set; }

        public string ClientName { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string BillingAddress { get; set; } = string.Empty;

        public DateOnly InvoiceDate { get; set; }

        public DateOnly DueDate { get; set; }

        public DateOnly? ServicePeriodStart { get; set; }

        public DateOnly? ServicePeriodEnd { get; set; }

        public string Currency { get; set; } = string.Empty;

        public string Branch { get; set; } = string.Empty;

        public string PaymentTerms { get; set; } = string.Empty;

        public string PreferredPaymentMode { get; set; } = string.Empty;

        public string Reference { get; set; } = string.Empty;

        public decimal SubTotal { get; set; }

        public decimal DiscountTotal { get; set; }

        public decimal TaxTotal { get; set; }

        public decimal GrandTotal { get; set; }

        public SalesInvoiceStatus Status { get; set; }

        public string Notes { get; set; } = string.Empty;

        public string AdditionalTerms { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public DateTime? IssuedDate { get; set; }

        public DateTime? VoidedDate { get; set; }

        public string VoidReason { get; set; } = string.Empty;

        public byte[] RowVersion { get; set; } = [];

        public List<SalesInvoiceItemResponseDto> Items { get; set; } =
            new List<SalesInvoiceItemResponseDto>();
    }
}