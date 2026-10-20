using System.ComponentModel.DataAnnotations;

namespace VendorCreditService.API.DTOs
{
    public class CreditBillBalanceDto
    {
        public int Id { get; set; }

        public string? BillNumber { get; set; }

        public string VendorInvoiceNumber { get; set; } = "";

        public decimal GrandTotal { get; set; }

        public decimal PaidAmount { get; set; }

        public decimal AppliedCreditAmount { get; set; }

        public decimal OutstandingAmount => GrandTotal - PaidAmount - AppliedCreditAmount;

        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        public DateOnly BillDate { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }
}
