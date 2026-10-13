using System.ComponentModel.DataAnnotations;

namespace ClientService.API.Models
{
    public class PaymentTerm
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int DueDays { get; set; }

        public bool IsActive { get; set; } = true;

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}