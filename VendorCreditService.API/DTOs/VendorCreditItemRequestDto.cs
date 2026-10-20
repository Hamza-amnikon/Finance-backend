using System.ComponentModel.DataAnnotations;
using VendorCreditService.API.Models;

namespace VendorCreditService.API.DTOs
{
    public class VendorCreditItemRequestDto : IValidatableObject
    {
        [Required]
        [MaxLength(200)]
        public string ItemName { get; set; } = "";

        [MaxLength(1000)]
        public string Description { get; set; } = "";

        [EnumDataType(typeof(PurchaseUnit))]
        public PurchaseUnit Unit { get; set; } =
            PurchaseUnit.Piece;

        public decimal Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public bool DiscountEnabled { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal TaxRate { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext context)
        {
            if (Quantity <= 0)
            {
                yield return new ValidationResult(
                    "Quantity must be greater than zero.",
                    new[] { nameof(Quantity) });
            }

            if (UnitPrice < 0)
            {
                yield return new ValidationResult(
                    "Unit price cannot be negative.",
                    new[] { nameof(UnitPrice) });
            }

            if (TaxRate < 0 || TaxRate > 100)
            {
                yield return new ValidationResult(
                    "Tax must be between 0 and 100 percent.",
                    new[] { nameof(TaxRate) });
            }

            if (DiscountEnabled)
            {
                if (DiscountAmount < 0)
                {
                    yield return new ValidationResult(
                        "Discount amount cannot be negative.",
                        new[] { nameof(DiscountAmount) });
                }
            }
        }
    }
}