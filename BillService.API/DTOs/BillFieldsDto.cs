using System.ComponentModel.DataAnnotations;
using BillService.API.Models;

namespace BillService.API.DTOs
{
    public class BillFieldsDto : IValidatableObject
    {
        [MaxLength(200)] public string VendorInvoiceNumber { get; set; } = "";
        public Guid VendorId { get; set; }
        public DateOnly BillDate { get; set; }
        public DateOnly? DueDate { get; set; }
        [Required, MaxLength(20)] public string Branch { get; set; } = "";
        [Required, RegularExpression("^[A-Za-z]{3}$")] public string Currency { get; set; } = "";
        [MaxLength(1000)] public string? BillingAddress { get; set; }
        [MaxLength(1000)] public string ShippingAddress { get; set; } = "";
        [MaxLength(200)] public string Reference { get; set; } = "";
        [MaxLength(200)] public string PaymentTerms { get; set; } = "";
        [MaxLength(2000)] public string Notes { get; set; } = "";
        [MaxLength(2000)] public string AdditionalTerms { get; set; } = "";
        [Required, MinLength(1), MaxLength(100)]
        public List<BillItemRequestDto> Items { get; set; } = [];

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            if (VendorId == Guid.Empty) yield return new("Select a vendor.", [nameof(VendorId)]);
            if (BillDate == default) yield return new("Bill date is required.", [nameof(BillDate)]);
            if (DueDate.HasValue && DueDate.Value < BillDate)
                yield return new("Due date cannot precede the bill date.", [nameof(DueDate)]);
        }
    }
}
