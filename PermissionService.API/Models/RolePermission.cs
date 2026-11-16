using System.ComponentModel.DataAnnotations;

namespace PermissionService.API.Models
{
    public class RolePermission
    {
        [Key]
        public int RolePermissionId { get; set; }

        [Range(1, int.MaxValue)]
        public int RoleId { get; set; }

        [Range(1, int.MaxValue)]
        public int PermissionId { get; set; }

        public bool CanView { get; set; }

        public bool CanCreate { get; set; }

        public bool CanEdit { get; set; }

        public bool CanDelete { get; set; }

        public bool CanApprove { get; set; }

        public bool CanExport { get; set; }

        public bool CanIssue { get; set; }

        public bool CanPost { get; set; }

        public bool CanVoid { get; set; }

        public bool CanReverse { get; set; }

        public bool CanReconcile { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}
