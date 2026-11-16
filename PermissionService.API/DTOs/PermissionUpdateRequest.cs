using System.ComponentModel.DataAnnotations;

namespace PermissionService.API.DTOs
{
    public class PermissionUpdateRequest : PermissionRequest
    {
        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];
    }
}