using System.ComponentModel.DataAnnotations;

namespace PaymentMadeService.API.DTOs
{
    public class BillPaymentBalanceDto
    {
        public int Id { get; set; }

        public Guid VendorId { get; set; }

        public string VendorName { get; set; } = "";

        public string? BillNumber { get; set; }

        public string VendorInvoiceNumber { get; set; } = "";

        public string Branch { get; set; } = "";

        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        public DateOnly BillDate { get; set; }

        public string Status { get; set; } = "";

        public decimal GrandTotal { get; set; }

        public decimal PaidAmount { get; set; }

        public decimal AppliedCreditAmount { get; set; }

        public decimal OutstandingAmount =>
            GrandTotal - PaidAmount - AppliedCreditAmount;

        public string PaymentStatus =>
            OutstandingAmount == 0
                ? AppliedCreditAmount > 0 ? "Settled" : "Paid"
                : PaidAmount == 0 && AppliedCreditAmount == 0
                    ? "Unpaid"
                    : AppliedCreditAmount > 0 ? "Partially settled" : "Partially paid";

        public byte[] RowVersion { get; set; } = [];
    }
}
