using System.ComponentModel.DataAnnotations;
using RoleService.API.Models;

namespace RoleService.API.DTOs
{
    public class RoleRequest
    {
        public int? RoleId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Role { get; set; } = string.Empty;

        [Required]
        [RegularExpression(RoleStatus.ValidationPattern)]
        public string Status { get; set; } = RoleStatus.Active;

        public byte[]? RowVersion { get; set; }
    }
}
