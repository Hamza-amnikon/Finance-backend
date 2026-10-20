using System.ComponentModel.DataAnnotations;

namespace VendorCreditService.API.DTOs
{
    public class CreateVendorCreditDto : VendorCreditFieldsDto
    {
        public Guid RequestId { get; set; }

        public override IEnumerable<ValidationResult> Validate(
            ValidationContext context)
        {
            foreach (var result in base.Validate(context))
            {
                yield return result;
            }

            if (RequestId == Guid.Empty)
            {
                yield return new ValidationResult(
                    "A request ID is required.",
                    new[] { nameof(RequestId) });
            }
        }
    }
}