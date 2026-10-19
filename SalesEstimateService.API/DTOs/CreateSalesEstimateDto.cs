using System.ComponentModel.DataAnnotations;

namespace SalesEstimateService.API.DTOs
{
    public class CreateSalesEstimateDto : IValidatableObject
    {
        public Guid ClientId { get; set; }

        public DateOnly EstimateDate { get; set; }

        public DateOnly ValidUntil { get; set; }

        public DateOnly? ServicePeriodStart { get; set; }

        public DateOnly? ServicePeriodEnd { get; set; }

        [Required]
        [RegularExpression("^[A-Za-z]{3}$")]
        public string Currency { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Branch { get; set; } = string.Empty;

        // Optional override; otherwise use the client's address.
        [MaxLength(1000)]
        public string? BillingAddress { get; set; }

        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string AdditionalTerms { get; set; } = string.Empty;

        [Required]
        [MinLength(1)]
        public List<SalesEstimateItemRequestDto> Items { get; set; } =
            new List<SalesEstimateItemRequestDto>();

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (ClientId == Guid.Empty)
            {
                yield return new ValidationResult(
                    "Select a client.",
                    new[] { nameof(ClientId) });
            }

            if (EstimateDate == default)
            {
                yield return new ValidationResult(
                    "Estimate date is required.",
                    new[] { nameof(EstimateDate) });
            }

            if (ValidUntil == default)
            {
                yield return new ValidationResult(
                    "Valid-until date is required.",
                    new[] { nameof(ValidUntil) });
            }
            else if (EstimateDate != default && ValidUntil < EstimateDate)
            {
                yield return new ValidationResult(
                    "Valid-until date cannot be before estimate date.",
                    new[] { nameof(ValidUntil) });
            }

            if (ServicePeriodStart.HasValue != ServicePeriodEnd.HasValue)
            {
                yield return new ValidationResult(
                    "Provide both service period dates, or leave both empty.",
                    new[]
                    {
                        nameof(ServicePeriodStart),
                        nameof(ServicePeriodEnd)
                    });
            }
            else if (
                ServicePeriodStart.HasValue &&
                ServicePeriodEnd.HasValue &&
                ServicePeriodEnd.Value < ServicePeriodStart.Value)
            {
                yield return new ValidationResult(
                    "Service period end cannot be before its start.",
                    new[] { nameof(ServicePeriodEnd) });
            }
        }
    }
}