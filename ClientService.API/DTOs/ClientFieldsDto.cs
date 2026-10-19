using System.ComponentModel.DataAnnotations;

namespace ClientService.API.DTOs
{
    public class ClientFieldsDto : IValidatableObject
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(254)]
        public string? Email { get; set; }

        [MaxLength(25)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Address { get; set; } = string.Empty;

        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [MaxLength(100)]
        public string State { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Country { get; set; } = string.Empty;

        [MaxLength(20)]
        public string PostalCode { get; set; } = string.Empty;

        [MaxLength(15)]
        public string GstNumber { get; set; } = string.Empty;

        [MaxLength(10)]
        public string Pan { get; set; } = string.Empty;

        [MaxLength(100)]
        public string BankName { get; set; } = string.Empty;

        [MaxLength(34)]
        public string AccountNumber { get; set; } = string.Empty;

        [MaxLength(11)]
        public string IfscCode { get; set; } = string.Empty;

        // UAE details.
        public bool? IsVatRegistered { get; set; }

        [MaxLength(15)]
        public string VatTrn { get; set; } = string.Empty;

        // US business tax ID.
        [MaxLength(10)]
        public string Ein { get; set; } = string.Empty;

        // Optional international bank details.
        [MaxLength(34)]
        public string Iban { get; set; } = string.Empty;

        [MaxLength(11)]
        public string SwiftCode { get; set; } = string.Empty;

        // Optional US bank routing number.
        [MaxLength(9)]
        public string RoutingNumber { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Branch { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^[A-Za-z]{3}$")]
        public string Currency { get; set; } = "AED";

        public bool IsActive { get; set; } = true;

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            var country = (Country ?? string.Empty).Trim();

            if ((country.Equals("United Arab Emirates", StringComparison.OrdinalIgnoreCase) ||
                 country.Equals("AE", StringComparison.OrdinalIgnoreCase) ||
                 country.Equals("UAE", StringComparison.OrdinalIgnoreCase)) &&
                IsVatRegistered == true &&
                string.IsNullOrWhiteSpace(VatTrn))
            {
                yield return new ValidationResult(
                    "VAT TRN is required for a VAT-registered UAE client.",
                    new[] { nameof(VatTrn) });
            }
        }
    }
}