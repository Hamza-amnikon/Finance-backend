using System.ComponentModel.DataAnnotations;

namespace PermissionService.API.Models
{
    public class Permission
    {
        [Key]
        public int PermissionId { get; set; }

        [Required]
        [MaxLength(100)]
        public string PermissionCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Module { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string PageName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Route { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [RegularExpression(PermissionStatus.ValidationPattern)]
        public string Status { get; set; } = PermissionStatus.Active;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}
