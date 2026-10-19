using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.DTOs
{
    public class CreateSalesCreditNoteDto : IValidatableObject
    {
        public Guid RequestId { get; set; }

        [Range(1, int.MaxValue)]
        public int SalesInvoiceId { get; set; }

        public DateOnly CreditNoteDate { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;

        [Required]
        [MinLength(1)]
        public List<SalesCreditNoteItemRequestDto> Items { get; set; }
            = [];

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (RequestId == Guid.Empty)
            {
                yield return new ValidationResult(
                    "A request ID is required.",
                    new[] { nameof(RequestId) });
            }

            if (CreditNoteDate == default)
            {
                yield return new ValidationResult(
                    "Credit note date is required.",
                    new[] { nameof(CreditNoteDate) });
            }

            if (Items != null)
            {
                if (Items.Any(item => item == null))
                {
                    yield return new ValidationResult(
                        "Credit note items cannot contain empty entries.",
                        new[] { nameof(Items) });
                }
                else if (Items
                    .GroupBy(item => item.SalesInvoiceItemId)
                    .Any(group => group.Count() > 1))
                {
                    yield return new ValidationResult(
                        "Select each invoice service only once.",
                        new[] { nameof(Items) });
                }
            }
        }
    }
}