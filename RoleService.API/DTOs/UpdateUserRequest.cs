using System.ComponentModel.DataAnnotations;

namespace RoleService.API.DTOs
{
    public class UpdateUserRequest
    {
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

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];
    }
}
