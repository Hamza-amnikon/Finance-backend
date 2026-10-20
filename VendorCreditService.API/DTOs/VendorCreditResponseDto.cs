using VendorCreditService.API.Models;

namespace VendorCreditService.API.DTOs
{
    public class VendorCreditResponseDto
    {
        public int Id { get; set; }

        public Guid RequestId { get; set; }

        public Guid VendorId { get; set; }

        public int? BillId { get; set; }

        public string? VendorCreditNumber { get; set; }

        public string VendorCreditNoteNumber { get; set; } = "";

        public string VendorName { get; set; } = "";

        public string CompanyName { get; set; } = "";

        public string BillingAddress { get; set; } = "";

        public DateOnly CreditDate { get; set; }

        public string Reference { get; set; } = "";

        public string Reason { get; set; } = "";

        public string Notes { get; set; } = "";

        public string Branch { get; set; } = "";

        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }

        public decimal SubTotal { get; set; }

        public decimal DiscountTotal { get; set; }

        public decimal TaxTotal { get; set; }

        public decimal GrandTotal { get; set; }

        public decimal AppliedAmount { get; set; }

        public decimal RemainingAmount => GrandTotal - AppliedAmount;

        public decimal AvailableAmount =>
            Status == VendorCreditStatus.Issued ? RemainingAmount : 0m;

        public List<CreditApplicationResponseDto> Applications { get; set; } = [];

        public VendorCreditStatus Status { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public DateTime? IssuedDate { get; set; }

        public DateTime? CancelledDate { get; set; }

        public byte[] RowVersion { get; set; } = [];

        public List<VendorCreditItemResponseDto> Items { get; set; } = [];
    }
}
