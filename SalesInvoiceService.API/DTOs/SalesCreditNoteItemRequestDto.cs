using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.DTOs
{
    public class SalesCreditNoteItemRequestDto : IValidatableObject
    {
        [Range(1, int.MaxValue)]
        public int SalesInvoiceItemId { get; set; }

        public decimal Quantity { get; set; }

        public decimal? CreditAmount { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            const decimal maximum = 99999999999999.9999m;
            if (CreditAmount.HasValue)
            {
                if (Quantity != 0 || CreditAmount <= 0 || CreditAmount > maximum)
                    yield return new ValidationResult("Enter a positive credit amount and leave quantity at zero.");
            }
            else if (Quantity <= 0 || Quantity > maximum || decimal.Round(Quantity, 4) != Quantity)
                yield return new ValidationResult("Credit quantity must be positive with at most four decimal places.");
        }
    }
}
