using System.ComponentModel.DataAnnotations;

namespace PurchaseOrderService.API.DTOs
{
    public class PurchaseOrderFieldsDto : IValidatableObject
    {
        public Guid VendorId { get; set; }

        public DateOnly OrderDate { get; set; }

        public DateOnly? ExpectedDeliveryDate { get; set; }

        [Required]
        [MaxLength(20)]
        public string Branch { get; set; } = "";

        [Required]
        [RegularExpression("^[A-Za-z]{3}$")]
        public string Currency { get; set; } = "";

        [MaxLength(1000)]
        public string? BillingAddress { get; set; }

        [MaxLength(1000)]
        public string ShippingAddress { get; set; } = "";

        [MaxLength(200)]
        public string Reference { get; set; } = "";

        [MaxLength(200)]
        public string PaymentTerms { get; set; } = "";

        [Range(1, int.MaxValue)]
        public int? PaymentModeId { get; set; }

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        [MaxLength(2000)]
        public string AdditionalTerms { get; set; } = "";

        [Required]
        [MinLength(1)]
        [MaxLength(100)]
        public List<PurchaseOrderItemRequestDto> Items { get; set; } = [];

        public IEnumerable<ValidationResult> Validate(
            ValidationContext context)
        {
            if (VendorId == Guid.Empty)
            {
                yield return new ValidationResult(
                    "Select a vendor.",
                    new[] { nameof(VendorId) });
            }

            if (OrderDate == default)
            {
                yield return new ValidationResult(
                    "Order date is required.",
                    new[] { nameof(OrderDate) });
            }

            if (
                ExpectedDeliveryDate.HasValue &&
                ExpectedDeliveryDate.Value < OrderDate)
            {
                yield return new ValidationResult(
                    "Delivery date cannot precede the order date.",
                    new[] { nameof(ExpectedDeliveryDate) });
            }
        }
    }
}