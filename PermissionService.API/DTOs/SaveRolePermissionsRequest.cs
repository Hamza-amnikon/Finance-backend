using System.ComponentModel.DataAnnotations;

namespace PermissionService.API.DTOs
{
    public class SaveRolePermissionsRequest
    {
        [Range(1, int.MaxValue)]
        public int RoleId { get; set; }

        [Required]
        public List<RolePermissionRequest> Permissions { get; set; } = [];
    }
}