using System.ComponentModel.DataAnnotations;
using VendorCreditService.API.Models;

namespace VendorCreditService.API.DTOs
{
    public class VendorCreditQueryDto : IValidatableObject
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 25;

        [MaxLength(200)]
        public string? Search { get; set; }

        [EnumDataType(typeof(VendorCreditStatus))]
        public VendorCreditStatus? Status { get; set; }

        public Guid? VendorId { get; set; }

        public DateOnly? FromDate { get; set; }

        public DateOnly? ToDate { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext context)
        {
            if (VendorId.HasValue && VendorId.Value == Guid.Empty)
            {
                yield return new ValidationResult(
                    "Select a valid vendor.",
                    new[] { nameof(VendorId) });
            }

            if (
                FromDate.HasValue &&
                ToDate.HasValue &&
                FromDate.Value > ToDate.Value)
            {
                yield return new ValidationResult(
                    "From date cannot be after to date.",
                    new[] { nameof(FromDate), nameof(ToDate) });
            }
        }
    }
}