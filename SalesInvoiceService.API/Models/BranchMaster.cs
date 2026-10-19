using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.Models
{
    public class BranchMaster
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        [MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Address { get; set; } = string.Empty;

        [MaxLength(2)]
        public string CountryCode { get; set; } = string.Empty;

        [MaxLength(50)]
        public string CompanyRegistrationNumber { get; set; } = string.Empty;

        [MaxLength(50)]
        public string TaxRegistrationNumber { get; set; } = string.Empty;

        [MaxLength(200)]
        public string BankAccountName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string BankName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string BankAccountNumber { get; set; } = string.Empty;

        [MaxLength(34)]
        public string BankIban { get; set; } = string.Empty;

        [MaxLength(11)]
        public string BankSwiftCode { get; set; } = string.Empty;

        [MaxLength(25)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(254)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string InvoiceNotes { get; set; } = string.Empty;

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}