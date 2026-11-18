using System.ComponentModel.DataAnnotations;

namespace RoleService.API.DTOs
{
    public class UpdateMicrosoftIdentityRequest
    {
        public Guid ObjectId { get; set; }

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];
    }
}
