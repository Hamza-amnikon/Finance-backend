using System.ComponentModel.DataAnnotations;

namespace ClientService.API.Models
{
    public class Client
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

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

        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Branch { get; set; } = "Dubai";

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = "AED";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}