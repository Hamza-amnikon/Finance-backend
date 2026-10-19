using System.ComponentModel.DataAnnotations;

namespace VendorService.API.DTOs
{
    public class CreateVendorDto
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? CompanyName { get; set; }

        [StringLength(200)]
        public string? ContactPerson { get; set; }

        [EmailAddress]
        [StringLength(254)]
        public string? Email { get; set; }

        [StringLength(25)]
        public string? Phone { get; set; }

        [StringLength(1000)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100)]
        public string? State { get; set; }

        [StringLength(100)]
        public string? Country { get; set; }

        [StringLength(20)]
        public string? PostalCode { get; set; }

        // India
        [StringLength(15)]
        public string? GstNumber { get; set; }

        [StringLength(10)]
        public string? Pan { get; set; }

        // UAE
        public bool? IsVatRegistered { get; set; }

        [StringLength(15)]
        public string? VatTrn { get; set; }

        // USA
        [StringLength(10)]
        public string? Ein { get; set; }

        // Bank details
        [StringLength(100)]
        public string? BankName { get; set; }

        [StringLength(34)]
        public string? AccountNumber { get; set; }

        [StringLength(11)]
        public string? IfscCode { get; set; }

        [StringLength(34)]
        public string? Iban { get; set; }

        [StringLength(11)]
        public string? SwiftCode { get; set; }

        [StringLength(9)]
        public string? RoutingNumber { get; set; }

        [StringLength(200)]
        public string? PaymentTerms { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        [Required]
        [StringLength(20)]
        public string Branch { get; set; } = string.Empty;

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = string.Empty;
    }
}