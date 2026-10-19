using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.DTOs
{
    public class SalesCreditNoteResponseDto
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        public int SalesInvoiceId { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;

        public string? CreditNoteNumber { get; set; }

        public Guid ClientId { get; set; }

        public string ClientName { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string BillingAddress { get; set; } = string.Empty;

        public string Branch { get; set; } = string.Empty;

        public string Currency { get; set; } = string.Empty;

        public DateOnly CreditNoteDate { get; set; }

        public string Reason { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public decimal SubTotal { get; set; }

        public decimal DiscountTotal { get; set; }

        public decimal TaxTotal { get; set; }

        public decimal GrandTotal { get; set; }

        public SalesCreditNoteStatus Status { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? IssuedDate { get; set; }

        public byte[] RowVersion { get; set; } = [];

        public List<SalesCreditNoteItemResponseDto> Items { get; set; }
            = [];
    }
}