namespace SalesInvoiceService.API.DTOs
{
    public class SalesInvoicePaymentSummaryDto
    {
        public int SalesInvoiceId { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;

        public string Currency { get; set; } = string.Empty;

        // Original invoice total.
        public decimal InvoiceTotal { get; set; }

        // Total of issued credit notes only.
        public decimal CreditedAmount { get; set; }

        // Invoice total minus issued credits.
        public decimal AdjustedInvoiceTotal { get; set; }

        public decimal PaidAmount { get; set; }

        // Amount the client still needs to pay.
        public decimal BalanceDue { get; set; }

        // Excess payment remaining after credits are applied.
        public decimal CreditBalance { get; set; }

        public string PaymentStatus { get; set; } = string.Empty;

        public List<SalesInvoicePaymentResponseDto> Payments { get; set; }
            = new List<SalesInvoicePaymentResponseDto>();
    }
}