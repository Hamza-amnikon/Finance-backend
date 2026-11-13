using System.ComponentModel.DataAnnotations;

namespace PermissionService.API.DTOs
{
    public class RolePermissionRequest
    {
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
    }
}