namespace VendorService.API.DTOs
{
    public class VendorResponseDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? CompanyName { get; set; }

        public string? ContactPerson { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? Address { get; set; }

        public string? City { get; set; }

        public string? State { get; set; }

        public string? Country { get; set; }

        public string? PostalCode { get; set; }

        // India
        public string? GstNumber { get; set; }

        public string? Pan { get; set; }

        // UAE
        public bool? IsVatRegistered { get; set; }

        public string? VatTrn { get; set; }

        // USA
        public string? Ein { get; set; }

        // Bank details
        public string? BankName { get; set; }

        public string? AccountNumber { get; set; }

        public string? IfscCode { get; set; }

        public string? Iban { get; set; }

        public string? SwiftCode { get; set; }

        public string? RoutingNumber { get; set; }

        public string? PaymentTerms { get; set; }

        public string? Notes { get; set; }

        public string Branch { get; set; } = string.Empty;

        public string Currency { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }
}