using System.ComponentModel.DataAnnotations;

namespace RoleService.API.Models
{
    public class User
    {
        [Key]
        public Guid UserId { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        public Guid ObjectId { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(254)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int RoleId { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}
