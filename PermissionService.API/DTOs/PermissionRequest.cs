using System.ComponentModel.DataAnnotations;
using PermissionService.API.Models;

namespace PermissionService.API.DTOs
{
    public class PermissionRequest
    {
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
    }
}