using System.ComponentModel.DataAnnotations;

namespace RoleService.API.Models
{
    public class Roles
    {
        [Key]
        public int RoleId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Role { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = RoleStatus.Active;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}
