using System.ComponentModel.DataAnnotations;

namespace VendorCreditService.API.DTOs
{
    public class VendorCreditFieldsDto : IValidatableObject
    {
        public Guid VendorId { get; set; }

        [Range(1, int.MaxValue)]
        public int? BillId { get; set; }

        public DateOnly CreditDate { get; set; }

        [MaxLength(200)]
        public string VendorCreditNoteNumber { get; set; } = "";

        [Required]
        [MaxLength(20)]
        public string Branch { get; set; } = "";

        [Required]
        [RegularExpression("^[A-Za-z]{3}$")]
        public string Currency { get; set; } = "";

        [MaxLength(1000)]
        public string? BillingAddress { get; set; }

        [MaxLength(200)]
        public string Reference { get; set; } = "";

        [MaxLength(1000)]
        public string Reason { get; set; } = "";

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        [Required]
        [MinLength(1)]
        [MaxLength(100)]
        public List<VendorCreditItemRequestDto> Items { get; set; } = [];

        public virtual IEnumerable<ValidationResult> Validate(
    ValidationContext context)
        {
            if (VendorId == Guid.Empty)
            {
                yield return new ValidationResult(
                    "Select a vendor.",
                    new[] { nameof(VendorId) });
            }

            if (CreditDate == default)
            {
                yield return new ValidationResult(
                    "Credit date is required.",
                    new[] { nameof(CreditDate) });
            }
        }
    }
}